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

        public CitasController(CitaService citaService, BarberSaaSDbContext context, HorarioLaboralService horarioService)
        {
            _citaService = citaService;
            _context = context;
            _horarioService = horarioService;
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
            var barbero = await _context.Barberos.FindAsync(barberoId);
            if (barbero == null) return NotFound("Barbero no encontrado.");

            var servicio = await _context.Servicios.FindAsync(servicioId);
            if (servicio == null) return NotFound("Servicio no encontrado.");
            int duracionTotalRequerida = servicio.DuracionMinutos;

            var citasExistentes = await _context.Citas
                .Where(c => c.BarberoId == barberoId
                         && c.FechaHora.Date == fechaSeleccionada.Date
                         && c.Estado != "Cancelada")
                .Include(c => c.Servicio)
                .ToListAsync();

            int nDiaSemana = (int)fechaSeleccionada.DayOfWeek;

            // 🔥 CORREGIDO: Llamada con la instancia '_horarioService' y pasando el 'barbero.TenantId'
            var horarioDelDia = await _horarioService.ObtenerHorarioDiaAsync(barbero.TenantId, nDiaSemana);

            if (horarioDelDia == null || horarioDelDia.EstaCerrado)
            {
                return Json(new List<string>());
            }

            var agendaService = new AgendaInteligenteService();
            List<DateTime> bloquesDisponibles = agendaService.ObtenerHorasDisponibles(citasExistentes,
                                                                                      fechaSeleccionada,
                                                                                      duracionTotalRequerida,
                                                                                      horarioDelDia);

            var horariosDisponiblesStrings = bloquesDisponibles.Select(h => h.ToString("hh:mm tt")).ToList();

            return Json(horariosDisponiblesStrings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,TenantId,ClienteId,BarberoId,ServicioId,FechaHora,Estado,Notas")] Cita cita)
        {
            if (CurrentUsuarioId == 0) return RedirectToAction("Login", "Account");

            if (!User.IsInRole("SuperAdmin"))
            {
                var usuarioDb = await _citaService.ObtenerClientePorIdAsync(CurrentUsuarioId);
                if (usuarioDb != null && usuarioDb.TenantId != null)
                {
                    cita.TenantId = usuarioDb.TenantId.Value;
                }
            }

            if (!User.IsInRole("SuperAdmin"))
            {
                ModelState.Remove(nameof(cita.TenantId));
            }

            ModelState.Remove(nameof(cita.Tenant));
            ModelState.Remove(nameof(cita.Cliente));
            ModelState.Remove(nameof(cita.Barbero));
            ModelState.Remove(nameof(cita.Servicio));

            if (ModelState.IsValid)
            {
                var servicio = await _context.Servicios.FindAsync(cita.ServicioId);
                if (servicio == null) return NotFound("El servicio seleccionado no existe.");

                DateTime inicioNuevaCita = cita.FechaHora;
                DateTime finNuevaCita = cita.FechaHora.AddMinutes(servicio.DuracionMinutos);

                int diaCita = (int)cita.FechaHora.DayOfWeek;

                // 🔥 CORREGIDO: Llamada con la instancia '_horarioService' y pasando el 'cita.TenantId'
                var horarioCita = await _horarioService.ObtenerHorarioDiaAsync(cita.TenantId, diaCita);
                TimeSpan horaCitaTime = cita.FechaHora.TimeOfDay;

                bool estaDentroDeJornada = false;

                if (horarioCita != null && !horarioCita.EstaCerrado)
                {
                    if (horarioCita.HoraInicio.HasValue && horarioCita.HoraFin.HasValue &&
                        horaCitaTime >= horarioCita.HoraInicio.Value && horaCitaTime < horarioCita.HoraFin.Value)
                    {
                        estaDentroDeJornada = true;
                    }
                    else if (horarioCita.HoraInicio2.HasValue && horarioCita.HoraFin2.HasValue &&
                             horaCitaTime >= horarioCita.HoraInicio2.Value && horaCitaTime < horarioCita.HoraFin2.Value)
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

                var citasCruce = await _context.Citas
                    .Where(c => c.FechaHora.Date == cita.FechaHora.Date
                             && c.Estado != "Cancelada"
                             && c.Id != cita.Id
                             && (c.BarberoId == cita.BarberoId || c.ClienteId == cita.ClienteId))
                    .Include(c => c.Servicio)
                    .ToListAsync();

                foreach (var c in citasCruce)
                {
                    int duracionExistente = c.Servicio?.DuracionMinutos ?? 30;
                    DateTime inicioExistente = c.FechaHora;
                    DateTime finExistente = c.FechaHora.AddMinutes(duracionExistente);

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

            if (User.IsInRole("SuperAdmin"))
            {
                ViewBag.TenantId = new SelectList(await _citaService.ObtenerTenantsAsync(), "Id", "NombreNegocio");
                ViewBag.ClienteId = new SelectList(await _citaService.ObtenerClientesAsync(), "Id", "Nombre");
                await CargarCombosPorRolYTenantAsync(isSuperAdmin: true);
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
                var barberosValidos = barberos.Where(b => b.Usuario != null).ToList();
                ViewBag.BarberoId = new SelectList(barberosValidos, "Id", "Usuario.Nombre");
                ViewBag.ServicioId = new SelectList(servicios, "Id", "Nombre");
                ViewBag.StrawberryField = barberosValidos;
            }
            else
            {
                var barberosSede = barberos.Where(b => b.TenantId == tenantId && b.Usuario != null).ToList();
                ViewBag.BarberoId = new SelectList(barberosSede, "Id", "Usuario.Nombre");

                var serviciosSede = servicios.Where(s => s.TenantId == tenantId).ToList();
                ViewBag.ServicioId = new SelectList(serviciosSede, "Id", "Nombre");
                ViewBag.StrawberryField = barberosSede;
            }
        }
    }
}