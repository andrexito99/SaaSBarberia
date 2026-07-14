using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;
using SaaSBarberia.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SaaSBarberia.Controllers
{
    [Authorize]
    public class CitasController : Controller
    {
        private readonly CitaService _citaService;
        private readonly BarberSaaSDbContext _context;
        private readonly HorarioLaboralService _horarioService;
        private readonly AgendaInteligenteService _agendaService;

        public CitasController(CitaService citaService, BarberSaaSDbContext context, HorarioLaboralService horarioService, AgendaInteligenteService agendaService)
        {
            _citaService = citaService;
            _context = context;
            _horarioService = horarioService;
            _agendaService = agendaService;
        }

        private int CurrentUsuarioId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId) ? userId : 0;
        private string CurrentUserIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        public async Task<IActionResult> Index()
        {
            if (CurrentUsuarioId == 0) return RedirectToAction("Login", "Account");

            var citas = await _citaService.ObtenerTodasConRelacionesAsync();

            if (User.IsInRole("Cliente"))
            {
                citas = citas.Where(c => c.ClienteId == CurrentUsuarioId).ToList();
            }
            else if (User.IsInRole("AdminBarberia") || User.IsInRole("Barbero"))
            {
                var empleado = await _citaService.ObtenerClientePorIdAsync(CurrentUsuarioId);
                if (empleado != null && empleado.TenantId != null)
                {
                    citas = citas.Where(c => c.TenantId == empleado.TenantId.Value).ToList();
                }
            }

            return View(citas);
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerHorariosDisponibles(int barberoId, int servicioId, string fecha)
        {
            if (barberoId == 0 || servicioId == 0 || string.IsNullOrEmpty(fecha))
            {
                return BadRequest("Datos insuficientes.");
            }

            if (!DateTime.TryParse(fecha, out DateTime fechaSeleccionada))
            {
                return BadRequest("Fecha inválida.");
            }

            // Buscamos el barbero para jalar el TenantId al que pertenece
            var barbero = await _context.Barberos
                .Include(b => b.Usuario)
                .FirstOrDefaultAsync(b => b.Id == barberoId && b.Usuario.Activo == true);
            if (barbero == null) return NotFound("Barbero no encontrado.");

            var servicio = await _context.Servicios.FirstOrDefaultAsync(s => s.Id == servicioId && s.TenantId == barbero.TenantId);
            if (servicio == null) return NotFound("Servicio no encontrado.");
            int duracionTotalRequerida = servicio.DuracionMinutos;

            var tenant = await _context.Tenants.FindAsync(barbero.TenantId);
            if (tenant == null || tenant.Activo != true) return Json(new List<string>());

            var citasExistentes = await _context.Citas
                .Where(c => c.BarberoId == barberoId
                         && c.FechaHora.Date == fechaSeleccionada.Date
                         && c.Estado != "Cancelada")
                .Include(c => c.Servicio)
                .ToListAsync();

            // 🔥 NUEVO: Traemos las ausencias del barbero para ese día específico
            var ausenciasActivas = await _context.BarberoAusencias
                .Where(a => a.TenantId == barbero.TenantId
                         && a.BarberoId == barberoId
                         && a.Activo
                         && a.FechaInicio.Date <= fechaSeleccionada.Date
                         && a.FechaFin.Date >= fechaSeleccionada.Date)
                .ToListAsync();

            int nDiaSemana = (int)fechaSeleccionada.DayOfWeek;

            // Llamada con la instancia '_horarioService' y pasando el 'barbero.TenantId'
            var horarioDelDia = await _horarioService.ObtenerHorarioDiaAsync(barbero.TenantId, nDiaSemana);

            if (horarioDelDia == null || horarioDelDia.EstaCerrado)
            {
                return Json(new List<string>());
            }

            // 🔥 CORREGIDO: Ahora sí le pasamos 'ausenciasActivas' en el segundo parámetro como lo espera el servicio
            List<DateTime> bloquesDisponibles = _agendaService.ObtenerHorasDisponibles(
                citasExistentes,
                ausenciasActivas, // <-- El parámetro que te faltaba cruzar
                fechaSeleccionada,
                duracionTotalRequerida,
                horarioDelDia,
                tenant.TiempoEntreCitas
            );

            var horariosDisponiblesStrings = bloquesDisponibles.Select(h => h.ToString("hh:mm tt")).ToList();

            return Json(horariosDisponiblesStrings);
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerOpcionesPorTenant(int tenantId)
        {
            if (tenantId <= 0) return BadRequest("Tenant inválido.");

            if (User.IsInRole("AdminBarberia") || User.IsInRole("Barbero"))
            {
                var usuarioDb = await _citaService.ObtenerClientePorIdAsync(CurrentUsuarioId);
                if (usuarioDb?.TenantId != tenantId) return Forbid();
            }

            var tenantExiste = await _context.Tenants.AnyAsync(t => t.Id == tenantId && t.Activo == true);
            if (!tenantExiste) return NotFound("Barbería no encontrada o inactiva.");

            var barberos = await _context.Barberos
                .Include(b => b.Usuario)
                .Where(b => b.TenantId == tenantId && b.Usuario.Activo == true)
                .OrderBy(b => b.Usuario.Nombre)
                .Select(b => new
                {
                    id = b.Id,
                    nombre = b.Usuario.Nombre,
                    especialidad = b.Especialidad ?? "Estilo libre",
                    rutaFoto = string.IsNullOrEmpty(b.RutaFoto) ? "/images/avatar-default.png" : b.RutaFoto
                })
                .ToListAsync();

            var servicios = await _context.Servicios
                .Where(s => s.TenantId == tenantId)
                .OrderBy(s => s.Nombre)
                .Select(s => new
                {
                    id = s.Id,
                    nombre = s.Nombre
                })
                .ToListAsync();

            return Json(new { barberos, servicios });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,TenantId,ClienteId,BarberoId,ServicioId,FechaHora,Estado,Notas")] Cita cita)
        {
            if (CurrentUsuarioId == 0) return RedirectToAction("Login", "Account");

            // 🔥 SOLUCIÓN 1: Si es Cliente, recuperamos el TenantId desde el Barbero que seleccionó
            if (User.IsInRole("Cliente"))
            {
                cita.ClienteId = CurrentUsuarioId; // Aseguramos que se guarde con su ID

                var barberoSeleccionado = await _context.Barberos.AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == cita.BarberoId);

                if (barberoSeleccionado != null)
                {
                    cita.TenantId = barberoSeleccionado.TenantId;
                }
            }
            else if (!User.IsInRole("SuperAdmin"))
            {
                // Administradores de sede o barberos
                var usuarioDb = await _citaService.ObtenerClientePorIdAsync(CurrentUsuarioId);
                if (usuarioDb != null && usuarioDb.TenantId != null)
                {
                    cita.TenantId = usuarioDb.TenantId.Value;
                }
            }

            // Limpiamos los errores del ModelState para datos autogenerados o de navegación
            if (!User.IsInRole("SuperAdmin"))
            {
                ModelState.Remove(nameof(cita.TenantId));
            }

            cita.Estado = "Pendiente"; // Forzar estado inicial seguro
            ModelState.Remove(nameof(cita.Estado));
            ModelState.Remove(nameof(cita.Tenant));
            ModelState.Remove(nameof(cita.Cliente));
            ModelState.Remove(nameof(cita.Barbero));
            ModelState.Remove(nameof(cita.Servicio));

            // 🕵️‍♂️ MODO DEBUG: Si sigue fallando por otra cosa, esto te lo cantará en la consola de Visual Studio
            if (!ModelState.IsValid)
            {
                var errores = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                foreach (var err in errores)
                {
                    System.Diagnostics.Debug.WriteLine($"🔴 ERROR DE VALIDACIÓN EN CITA: {err}");
                }
            }

            if (ModelState.IsValid)
            {
                var tenant = await _context.Tenants.FindAsync(cita.TenantId);
                if (tenant == null || tenant.Activo != true)
                {
                    ModelState.AddModelError(nameof(cita.TenantId), "La barbería seleccionada no está disponible.");
                    await RecargarCombosErrorAsync(cita);
                    return View(cita);
                }

                var barbero = await _context.Barberos
                    .Include(b => b.Usuario)
                    .FirstOrDefaultAsync(b => b.Id == cita.BarberoId && b.TenantId == cita.TenantId && b.Usuario.Activo == true);
                if (barbero == null)
                {
                    ModelState.AddModelError(nameof(cita.BarberoId), "El barbero seleccionado no pertenece a esta barbería o no está activo.");
                    await RecargarCombosErrorAsync(cita);
                    return View(cita);
                }

                var servicio = await _context.Servicios.FirstOrDefaultAsync(s => s.Id == cita.ServicioId && s.TenantId == cita.TenantId);
                if (servicio == null) return NotFound("El servicio seleccionado no existe.");

                DateTime inicioNuevaCita = cita.FechaHora;
                DateTime finNuevaCita = cita.FechaHora.AddMinutes(servicio.DuracionMinutos + Math.Max(0, tenant.TiempoEntreCitas));

                int diaCita = (int)cita.FechaHora.DayOfWeek;

                var horarioCita = await _horarioService.ObtenerHorarioDiaAsync(cita.TenantId, diaCita);
                TimeSpan horaCitaTime = cita.FechaHora.TimeOfDay;

                bool estaDentroDeJornada = false;

                if (horarioCita != null && !horarioCita.EstaCerrado)
                {
                    if (horarioCita.HoraInicio.HasValue && horarioCita.HoraFin.HasValue &&
                        horaCitaTime >= horarioCita.HoraInicio.Value && cita.FechaHora.AddMinutes(servicio.DuracionMinutos).TimeOfDay <= horarioCita.HoraFin.Value)
                    {
                        estaDentroDeJornada = true;
                    }
                    else if (horarioCita.HoraInicio2.HasValue && horarioCita.HoraFin2.HasValue &&
                             horaCitaTime >= horarioCita.HoraInicio2.Value && cita.FechaHora.AddMinutes(servicio.DuracionMinutos).TimeOfDay <= horarioCita.HoraFin2.Value)
                    {
                        estaDentroDeJornada = true;
                    }
                }

                if (!estaDentroDeJornada)
                {
                    ModelState.AddModelError("", "¡Error! El horario seleccionado está por fuera de la jornada laboral de la barbería.");
                    await RecargarCombosErrorAsync(cita);
                    return View(cita);
                }

                if (await _agendaService.TieneAusenciaEnRangoAsync(cita.TenantId, cita.BarberoId, inicioNuevaCita, finNuevaCita))
                {
                    ModelState.AddModelError("", "El barbero tiene una ausencia activa que bloquea el horario seleccionado.");
                    await RecargarCombosErrorAsync(cita);
                    return View(cita);
                }

                var citasCruce = await _context.Citas
                    .Where(c => c.FechaHora.Date == cita.FechaHora.Date
                             && c.Estado != "Cancelada"
                             && c.Id != cita.Id
                             && c.TenantId == cita.TenantId
                             && (c.BarberoId == cita.BarberoId || c.ClienteId == cita.ClienteId))
                    .Include(c => c.Servicio)
                    .ToListAsync();

                foreach (var c in citasCruce)
                {
                    int duracionExistente = c.Servicio?.DuracionMinutos ?? 30;
                    DateTime inicioExistente = c.FechaHora;
                    DateTime finExistente = c.FechaHora.AddMinutes(duracionExistente + Math.Max(0, tenant.TiempoEntreCitas));

                    if (inicioNuevaCita < finExistente && finNuevaCita > inicioExistente)
                    {
                        if (c.BarberoId == cita.BarberoId)
                        {
                            ModelState.AddModelError("", "Lo sentimos, el barbero ya se encuentra ocupado en ese horario.");
                        }
                        else
                        {
                            ModelState.AddModelError("", "¡Pilas! Este cliente ya tiene otra cita agendada que se cruza con este horario.");
                        }
                        await RecargarCombosErrorAsync(cita);
                        return View(cita);
                    }
                }

                await _citaService.GuardarAsync(cita, CurrentUsuarioId, CurrentUserIp);
                return RedirectToAction(nameof(Index));
            }

            await RecargarCombosErrorAsync(cita);
            return View(cita);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancelar(int id)
        {
            var cita = await _citaService.ObtenerPorIdAsync(id);
            if (cita == null) return NotFound();

            if (User.IsInRole("Cliente") && cita.ClienteId != CurrentUsuarioId)
            {
                return Forbid();
            }

            cita.Estado = "Cancelada";
            await _citaService.GuardarAsync(cita, CurrentUsuarioId, CurrentUserIp);

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var cita = await _citaService.ObtenerPorIdAsync(id.Value);
            if (cita == null) return NotFound();
            return View(cita);
        }

        public async Task<IActionResult> Create()
        {
            if (CurrentUsuarioId == 0) return RedirectToAction("Login", "Account");

            var nuevaCita = new Cita { FechaHora = DateTime.Now, Estado = "Pendiente" };

            if (User.IsInRole("SuperAdmin") || User.IsInRole("Cliente"))
            {
                ViewBag.TenantId = new SelectList(await _citaService.ObtenerTenantsAsync(), "Id", "NombreNegocio");
                if (User.IsInRole("Cliente"))
                {
                    nuevaCita.ClienteId = CurrentUsuarioId;
                }
                else
                {
                    ViewBag.ClienteId = new SelectList(await _citaService.ObtenerClientesAsync(), "Id", "Nombre");
                }

                await CargarCombosPorRolYTenantAsync(isSuperAdmin: true, tenantId: 0);
            }
            else
            {
                var usuarioDb = await _citaService.ObtenerClientePorIdAsync(CurrentUsuarioId);
                if (usuarioDb == null || usuarioDb.TenantId == null) return NotFound("Sede del usuario inválida o no asignada.");

                int tenantIdSede = usuarioDb.TenantId.Value;
                nuevaCita.TenantId = tenantIdSede;

                if (User.IsInRole("Cliente"))
                {
                    nuevaCita.ClienteId = CurrentUsuarioId;
                }
                else
                {
                    var clientesSede = (await _citaService.ObtenerClientesAsync()).Where(u => u.TenantId == tenantIdSede);
                    ViewBag.ClienteId = new SelectList(clientesSede, "Id", "Nombre");
                }

                await CargarCombosPorRolYTenantAsync(isSuperAdmin: false, tenantId: tenantIdSede);
            }

            return View(nuevaCita);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var cita = await _citaService.ObtenerPorIdAsync(id.Value);
            if (cita == null) return NotFound();

            ViewBag.TenantId = new SelectList(await _citaService.ObtenerTenantsAsync(), "Id", "NombreNegocio", cita.TenantId);
            ViewBag.ClienteId = new SelectList(await _citaService.ObtenerClientesAsync(), "Id", "Nombre", cita.ClienteId);

            var barberos = await _citaService.ObtenerBarberosAsync();
            ViewBag.BarberoId = new SelectList(barberos, "Id", "Usuario.Nombre", cita.BarberoId);
            ViewBag.ServicioId = new SelectList(await _citaService.ObtenerServiciosAsync(), "Id", "Nombre", cita.ServicioId);

            return View(cita);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,TenantId,ClienteId,BarberoId,ServicioId,FechaHora,Estado,Notas")] Cita cita)
        {
            if (id != cita.Id) return NotFound();

            ModelState.Remove(nameof(cita.Tenant));
            ModelState.Remove(nameof(cita.Cliente));
            ModelState.Remove(nameof(cita.Barbero));
            ModelState.Remove(nameof(cita.Servicio));

            if (ModelState.IsValid)
            {
                try
                {
                    await _citaService.GuardarAsync(cita, CurrentUsuarioId, CurrentUserIp);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Citas.Any(e => e.Id == cita.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(cita);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var cita = await _citaService.ObtenerPorIdAsync(id.Value);
            if (cita == null) return NotFound();
            return View(cita);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _citaService.EliminarAsync(id, CurrentUsuarioId, CurrentUserIp);
            return RedirectToAction(nameof(Index));
        }

        private async Task RecargarCombosErrorAsync(Cita cita)
        {
            if (User.IsInRole("SuperAdmin"))
            {
                ViewBag.TenantId = new SelectList(await _citaService.ObtenerTenantsAsync(), "Id", "NombreNegocio", cita.TenantId);
                ViewBag.ClienteId = new SelectList(await _citaService.ObtenerClientesAsync(), "Id", "Nombre", cita.ClienteId);
                await CargarCombosPorRolYTenantAsync(isSuperAdmin: true);
            }
            else
            {
                if (User.IsInRole("Cliente"))
                {
                    ViewBag.TenantId = new SelectList(await _citaService.ObtenerTenantsAsync(), "Id", "NombreNegocio", cita.TenantId);
                }

                if (!User.IsInRole("Cliente"))
                {
                    var clientesSede = (await _citaService.ObtenerClientesAsync()).Where(u => u.TenantId == cita.TenantId);
                    ViewBag.ClienteId = new SelectList(clientesSede, "Id", "Nombre", cita.ClienteId);
                }
                await CargarCombosPorRolYTenantAsync(isSuperAdmin: false, tenantId: cita.TenantId);
            }
        }

        private async Task CargarCombosPorRolYTenantAsync(bool isSuperAdmin, int tenantId = 0)
        {
            var barberos = await _citaService.ObtenerBarberosAsync();
            var servicios = await _citaService.ObtenerServiciosAsync();

            if (isSuperAdmin)
            {
                var barberosValidos = barberos
                    .Where(b => b.Usuario != null && b.Usuario.Activo == true && (tenantId == 0 || b.TenantId == tenantId))
                    .ToList();

                var serviciosValidos = servicios
                    .Where(s => tenantId == 0 || s.TenantId == tenantId)
                    .ToList();

                ViewBag.BarberoId = new SelectList(barberosValidos, "Id", "Usuario.Nombre");
                ViewBag.ServicioId = new SelectList(serviciosValidos, "Id", "Nombre");
                ViewBag.ListaBarberosCompleta = barberosValidos;
            }
            else
            {
                var barberosSede = barberos.Where(b => b.TenantId == tenantId && b.Usuario != null && b.Usuario.Activo == true).ToList();
                ViewBag.BarberoId = new SelectList(barberosSede, "Id", "Usuario.Nombre");

                var serviciosSede = servicios.Where(s => s.TenantId == tenantId).ToList();
                ViewBag.ServicioId = new SelectList(serviciosSede, "Id", "Nombre");
                ViewBag.ListaBarberosCompleta = barberosSede;
            }
        }
    }
}
