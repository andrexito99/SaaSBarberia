using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Data;
using SaaSBarberia.Data;
using SaaSBarberia.Models;

namespace SaaSBarberia.Services
{
    public class CitaService
    {
        private readonly BarberSaaSDbContext _context;
        private readonly ITenantProvider _tenantProvider;
        private readonly IAuditoriaService _auditoriaService; // 🚨 El vigilante inyectado
        private readonly IEventosSistemaService _eventos;
        private readonly HorarioLaboralService _horarioService;
        private readonly AgendaInteligenteService _agendaService;
        private readonly IWhatsAppService _whatsAppService;

        public CitaService(BarberSaaSDbContext context, IAuditoriaService auditoriaService, IEventosSistemaService eventos,
            HorarioLaboralService horarioService, AgendaInteligenteService agendaService, IWhatsAppService whatsAppService,
            ITenantProvider tenantProvider)
        {
            _context = context;
            _auditoriaService = auditoriaService;
            _eventos = eventos;
            _horarioService = horarioService;
            _agendaService = agendaService;
            _whatsAppService = whatsAppService;
            _tenantProvider = tenantProvider;
        }

        /// <summary>
        /// Valida y persiste la reserva dentro de una misma transacción serializable.
        /// La validación previa de la interfaz es solo informativa; esta es la barrera
        /// definitiva ante dos solicitudes simultáneas para el mismo barbero.
        /// </summary>
        public async Task GuardarValidandoDisponibilidadAsync(Cita cita, int usuarioId, string ip)
        {
            if (string.Equals(cita.Estado, "Cancelada", StringComparison.OrdinalIgnoreCase))
            {
                await GuardarAsync(cita, usuarioId, ip);
                return;
            }

            var esNueva = cita.Id == 0;
            var original = esNueva ? null : await _context.Citas.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cita.Id && c.TenantId == cita.TenantId);
            await using var transaccion = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await ValidarDisponibilidadAsync(cita);
            await GuardarAsync(cita, usuarioId, ip, enviarWhatsApp: false);
            await transaccion.CommitAsync();

            if (esNueva) await _whatsAppService.EnviarEventoCitaAsync(cita, "Confirmacion");
            else if (original != null && original.Estado != cita.Estado && cita.Estado == "Cancelada") await _whatsAppService.EnviarEventoCitaAsync(cita, "Cancelacion");
            else if (original != null && (original.FechaHora != cita.FechaHora || original.BarberoId != cita.BarberoId || original.ServicioId != cita.ServicioId)) await _whatsAppService.EnviarEventoCitaAsync(cita, "Reprogramacion");
        }

        private async Task ValidarDisponibilidadAsync(Cita cita)
        {
            var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Id == cita.TenantId && t.Activo == true)
                ?? throw new CitaNoDisponibleException("La barbería seleccionada no está disponible.");

            var barbero = await _context.Barberos
                .Include(b => b.Usuario)
                .FirstOrDefaultAsync(b => b.Id == cita.BarberoId && b.TenantId == cita.TenantId && b.Usuario.Activo == true);
            if (barbero == null)
                throw new CitaNoDisponibleException("El barbero seleccionado no pertenece a esta barbería o no está activo.");

            var servicio = await _context.Servicios.FirstOrDefaultAsync(s => s.Id == cita.ServicioId && s.TenantId == cita.TenantId);
            if (servicio == null || servicio.DuracionMinutos <= 0)
                throw new CitaNoDisponibleException("El servicio seleccionado no es válido para esta barbería.");

            var inicio = cita.FechaHora;
            var finServicio = inicio.AddMinutes(servicio.DuracionMinutos);
            var finConMargen = finServicio.AddMinutes(Math.Max(0, tenant.TiempoEntreCitas));
            var horario = await _horarioService.ObtenerHorarioDiaAsync(cita.TenantId, (int)inicio.DayOfWeek);

            bool dentroDeJornada = horario != null && !horario.EstaCerrado &&
                ((horario.HoraInicio.HasValue && horario.HoraFin.HasValue && inicio.TimeOfDay >= horario.HoraInicio && finServicio.TimeOfDay <= horario.HoraFin) ||
                 (horario.HoraInicio2.HasValue && horario.HoraFin2.HasValue && inicio.TimeOfDay >= horario.HoraInicio2 && finServicio.TimeOfDay <= horario.HoraFin2));
            if (!dentroDeJornada)
                throw new CitaNoDisponibleException("El horario seleccionado está por fuera de la jornada laboral de la barbería.");

            if (await _agendaService.TieneAusenciaEnRangoAsync(cita.TenantId, cita.BarberoId, inicio, finConMargen))
                throw new CitaNoDisponibleException("El barbero tiene una ausencia activa que bloquea el horario seleccionado.");

            // Consultas por rango (no por .Date) para que SQL Server use los índices de agenda
            // y Serializable proteja también el hueco donde se insertaría una nueva reserva.
            var inicioDia = inicio.Date;
            var finDia = inicioDia.AddDays(1);
            var citasDelBarbero = await _context.Citas
                .Where(c => c.TenantId == cita.TenantId && c.BarberoId == cita.BarberoId &&
                            c.FechaHora >= inicioDia && c.FechaHora < finDia &&
                            c.Estado != "Cancelada" && c.Id != cita.Id)
                .Include(c => c.Servicio)
                .ToListAsync();

            var citasDelCliente = await _context.Citas
                .Where(c => c.TenantId == cita.TenantId && c.ClienteId == cita.ClienteId &&
                            c.FechaHora >= inicioDia && c.FechaHora < finDia &&
                            c.Estado != "Cancelada" && c.Id != cita.Id && c.BarberoId != cita.BarberoId)
                .Include(c => c.Servicio)
                .ToListAsync();

            foreach (var existente in citasDelBarbero.Concat(citasDelCliente))
            {
                var finExistente = existente.FechaHora.AddMinutes((existente.Servicio?.DuracionMinutos ?? 30) + Math.Max(0, tenant.TiempoEntreCitas));
                if (inicio < finExistente && finConMargen > existente.FechaHora)
                {
                    var mensaje = existente.BarberoId == cita.BarberoId
                        ? "Lo sentimos, el barbero ya se encuentra ocupado en ese horario."
                        : "Este cliente ya tiene otra cita agendada que se cruza con este horario.";
                    throw new CitaNoDisponibleException(mensaje);
                }
            }
        }

        public async Task<List<Cita>> ObtenerTodasConRelacionesAsync()
        {
            return await _context.Citas
                .Where(c => _tenantProvider.EsSuperAdmin || c.TenantId == _tenantProvider.CurrentTenantId)
                .Include(c => c.Barbero)
                    .ThenInclude(b => b.Usuario)
                .Include(c => c.Cliente)
                .Include(c => c.Servicio)
                .Include(c => c.Tenant)
                .ToListAsync();
        }

        public async Task<Cita?> ObtenerPorIdAsync(int id)
        {
            return await _context.Citas
                .Include(c => c.Barbero)
                    .ThenInclude(b => b.Usuario)
                .Include(c => c.Cliente)
                .Include(c => c.Servicio)
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == id && (_tenantProvider.EsSuperAdmin || c.TenantId == _tenantProvider.CurrentTenantId));
        }

        // 💾 GUARDAR / ACTUALIZAR CON AUDITORÍA INCLUIDA
        public async Task GuardarAsync(Cita cita, int usuarioId, string ip, bool enviarWhatsApp = true)
        {
            if (cita.Id == 0)
            {
                cita.Estado ??= "Pendiente";
                _context.Add(cita);
                await _context.SaveChangesAsync(); // Guardamos primero para generar el Id

                // Traemos los datos cargados para armar una descripción mela
                var detalles = await ObtenerPorIdAsync(cita.Id);
                string barberoNom = detalles?.Barbero?.Usuario?.Nombre ?? "Desconocido";
                string clienteNom = detalles?.Cliente?.Nombre ?? "Desconocido";
                string servicioNom = detalles?.Servicio?.Nombre ?? "Desconocido";

                await _auditoriaService.RegistrarAsync(
                    cita.TenantId,
                    usuarioId,
                    "Cita",
                    cita.Id.ToString(),
                    "Crear",
                    $"Se creó una nueva cita para el {cita.FechaHora:dd/MM/yyyy hh:mm tt}. Cliente: {clienteNom}, Barbero: {barberoNom}, Servicio: {servicioNom}, Estado: {cita.Estado}",
                    ip
                );
                await _eventos.PublicarAsync(new EventoSistema(TipoEventoSistema.NuevaCita, cita.TenantId, "Nueva cita", $"Se agendó una cita para el {cita.FechaHora:dd/MM/yyyy HH:mm}.", "/Citas/Details/" + cita.Id, new[] { cita.ClienteId, detalles?.Barbero?.UsuarioId ?? 0 }.Where(id => id > 0).ToArray()));
                if (enviarWhatsApp) await _whatsAppService.EnviarEventoCitaAsync(cita, "Confirmacion");
            }
            else
            {
                // Obtenemos los valores originales para auditar cambios reales
                var original = await _context.Citas.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cita.Id);

                _context.Update(cita);
                await _context.SaveChangesAsync();

                string accionAuditoria = "Editar";
                string descripcion = $"Se modificó la cita ID {cita.Id}. ";

                if (original != null)
                {
                    if (original.Estado != cita.Estado)
                    {
                        descripcion += $"Cambio de Estado: '{original.Estado}' -> '{cita.Estado}'. ";
                        if (cita.Estado == "Cancelada") accionAuditoria = "Cancelar";
                    }
                    if (original.FechaHora != cita.FechaHora)
                        descripcion += $"Cambio de Fecha: {original.FechaHora:dd/MM/yyyy hh:mm tt} -> {cita.FechaHora:dd/MM/yyyy hh:mm tt}. ";
                    if (original.BarberoId != cita.BarberoId)
                        descripcion += $"Cambio de Barbero (ID): {original.BarberoId} -> {cita.BarberoId}. ";
                    if (original.ServicioId != cita.ServicioId)
                        descripcion += $"Cambio de Servicio (ID): {original.ServicioId} -> {cita.ServicioId}. ";
                }

                await _auditoriaService.RegistrarAsync(cita.TenantId, usuarioId, "Cita", cita.Id.ToString(), accionAuditoria, descripcion, ip);
                if (original != null && original.Estado != cita.Estado && cita.Estado == "Cancelada")
                {
                    await _eventos.PublicarAsync(new EventoSistema(TipoEventoSistema.CitaCancelada, cita.TenantId, "Cita cancelada", $"La cita del {cita.FechaHora:dd/MM/yyyy HH:mm} fue cancelada.", "/Citas/Details/" + cita.Id, new[] { cita.ClienteId }));
                    if (enviarWhatsApp) await _whatsAppService.EnviarEventoCitaAsync(cita, "Cancelacion");
                }
                else if (original != null && (original.FechaHora != cita.FechaHora || original.BarberoId != cita.BarberoId || original.ServicioId != cita.ServicioId))
                {
                    await _eventos.PublicarAsync(new EventoSistema(TipoEventoSistema.CitaReprogramada, cita.TenantId, "Cita reprogramada", $"La cita ID {cita.Id} fue modificada.", "/Citas/Details/" + cita.Id, new[] { cita.ClienteId }));
                    if (enviarWhatsApp) await _whatsAppService.EnviarEventoCitaAsync(cita, "Reprogramacion");
                }
            }
        }

        /// <summary>
        /// Marks an appointment as completed without accepting appointment fields
        /// from the browser. The tenant is supplied by the authenticated caller.
        /// </summary>
        public async Task<bool> CompletarAsync(int citaId, int tenantId, int usuarioId, string ip)
        {
            var targetTenantId = _tenantProvider.EsSuperAdmin ? tenantId : (_tenantProvider.CurrentTenantId ?? tenantId);
            var cita = await _context.Citas
                .FirstOrDefaultAsync(c => c.Id == citaId && c.TenantId == targetTenantId);

            if (cita == null || string.Equals(cita.Estado, "Cancelada", StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.Equals(cita.Estado, "Completada", StringComparison.OrdinalIgnoreCase))
                return true;

            cita.Estado = "Completada";
            await _context.SaveChangesAsync();
            await _auditoriaService.RegistrarAsync(
                cita.TenantId, usuarioId, "Cita", cita.Id.ToString(), "Completar",
                $"La cita ID {cita.Id} fue marcada como completada.", ip);
            return true;
        }

        // 🗑️ ELIMINACIÓN CON AUDITORÍA
        public async Task EliminarAsync(int id, int usuarioId, string ip)
        {
            var cita = await ObtenerPorIdAsync(id);
            if (cita != null)
            {
                int tenantId = cita.TenantId;
                string infoCita = $"Cita ID {cita.Id} de Cliente: {cita.Cliente?.Nombre ?? "Desconocido"} con Barbero: {cita.Barbero?.Usuario?.Nombre ?? "Desconocido"} programada para el {cita.FechaHora:dd/MM/yyyy hh:mm tt}.";

                // 1. Limpieza de facturas amarradas
                var facturasAsociadas = await _context.Set<Factura>()
                    .Where(f => f.CitaId == id)
                    .ToListAsync();

                if (facturasAsociadas.Any())
                {
                    _context.Set<Factura>().RemoveRange(facturasAsociadas);
                }

                // 2. Eliminación de la cita
                _context.Citas.Remove(cita);
                await _context.SaveChangesAsync();

                // 🔥 Guardamos el rastro imborrable
                await _auditoriaService.RegistrarAsync(
                    tenantId,
                    usuarioId,
                    "Cita",
                    id.ToString(),
                    "Eliminar",
                    $"Se ELIMINÓ por completo del sistema la {infoCita}",
                    ip
                );
            }
        }

        public bool Existe(int id)
        {
            return _context.Citas.Any(e => e.Id == id);
        }

        public async Task<Usuario?> ObtenerClientePorIdAsync(int id)
        {
            return await _context.Usuarios
                .Include(u => u.Tenant)
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<List<Tenant>> ObtenerTenantsAsync() => await _context.Tenants.ToListAsync();
        public async Task<List<Usuario>> ObtenerClientesAsync() => await _context.Usuarios.ToListAsync();
        public async Task<List<Barbero>> ObtenerBarberosAsync()
        {
            return await _context.Barberos
                .Include(b => b.Usuario)
                .ToListAsync();
        }
        public async Task<List<Servicio>> ObtenerServiciosAsync() => await _context.Servicios.ToListAsync();
    }
}
