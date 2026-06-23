using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;

namespace SaaSBarberia.Services
{
    public class CitaService
    {
        private readonly BarberSaaSDbContext _context;
        private readonly IAuditoriaService _auditoriaService; // 🚨 El vigilante inyectado

        public CitaService(BarberSaaSDbContext context, IAuditoriaService auditoriaService)
        {
            _context = context;
            _auditoriaService = auditoriaService;
        }

        public async Task<List<Cita>> ObtenerTodasConRelacionesAsync()
        {
            return await _context.Citas
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
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        // 💾 GUARDAR / ACTUALIZAR CON AUDITORÍA INCLUIDA
        public async Task GuardarAsync(Cita cita, int usuarioId, string ip)
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
                    $"Se agendó una nueva cita ID {cita.Id}. Cliente: {clienteNom}. Barbero: {barberoNom}. Servicio: {servicioNom}. Fecha/Hora: {cita.FechaHora:dd/MM/yyyy hh:mm tt}.",
                    ip
                );
            }
            else
            {
                // Obtenemos el original sin seguimiento para ver qué cambió o si fue cancelada
                var original = await _context.Citas
                    .AsNoTracking()
                    .Include(c => c.Servicio)
                    .FirstOrDefaultAsync(c => c.Id == cita.Id);

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
            }
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