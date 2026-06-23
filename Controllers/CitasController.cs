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

        public CitasController(CitaService citaService, BarberSaaSDbContext context)
        {
            _citaService = citaService;
            _context = context;
        }

        // 🔍 PROPIEDADES DINÁMICAS PARA LOGS Y AUDITORÍA
        private int CurrentUsuarioId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId) ? userId : 0;
        private string CurrentUserIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        // GET: Citas
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

            var servicio = await _context.Servicios.FindAsync(servicioId);
            if (servicio == null) return NotFound("Servicio no encontrado.");
            int duracionNuevaCita = servicio.DuracionMinutos;

            DateTime ahora = DateTime.Now;
            DateTime horaInicioJornada = fechaSeleccionada.Date.AddHours(8);

            if (fechaSeleccionada.Date == ahora.Date)
            {
                if (ahora > horaInicioJornada)
                {
                    horaInicioJornada = ahora;
                }
            }

            DateTime horaFinJornada = fechaSeleccionada.Date.AddHours(19).AddMinutes(0);

            var citasExistentes = await _context.Citas
                .Where(c => c.BarberoId == barberoId
                         && c.FechaHora.Date == fechaSeleccionada.Date
                         && c.Estado != "Cancelada")
                .Include(c => c.Servicio)
                .OrderBy(c => c.FechaHora)
                .ToListAsync();

            List<string> horariosDisponibles = new List<string>();
            int intervaloBloques = 20;
            DateTime bloqueEvaluar = horaInicioJornada;

            while (bloqueEvaluar.AddMinutes(duracionNuevaCita) <= horaFinJornada)
            {
                DateTime inicioNuevo = bloqueEvaluar;
                DateTime finNuevo = bloqueEvaluar.AddMinutes(duracionNuevaCita);
                bool cruza = false;

                foreach (var cita in citasExistentes)
                {
                    int duracionExistente = cita.Servicio?.DuracionMinutos ?? 30;
                    DateTime inicioExistente = cita.FechaHora;
                    DateTime finExistente = cita.FechaHora.AddMinutes(duracionExistente);

                    if (inicioNuevo < finExistente && finNuevo > inicioExistente)
                    {
                        cruza = true;
                        break;
                    }
                }

                if (!cruza)
                {
                    horariosDisponibles.Add(inicioNuevo.ToString("hh:mm tt"));
                }

                bloqueEvaluar = bloqueEvaluar.AddMinutes(intervaloBloques);
            }

            return Json(horariosDisponibles);
        }

        // POST: Citas/Create
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

                // Envíamos usuario e IP al servicio
                await _citaService.GuardarAsync(cita, CurrentUsuarioId, CurrentUserIp);
                return RedirectToAction(nameof(Index));
            }

            await RecargarCombosErrorAsync(cita);
            return View(cita);
        }

        // POST: Citas/Cancelar
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
            // GuardarAsync detectará el cambio de estado y lo auditará automáticamente como "Cancelar"
            await _citaService.GuardarAsync(cita, CurrentUsuarioId, CurrentUserIp);

            return RedirectToAction(nameof(Index));
        }

        // GET: Citas/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var cita = await _citaService.ObtenerPorIdAsync(id.Value);
            if (cita == null) return NotFound();
            return View(cita);
        }

        // GET: Citas/Create
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

        // GET: Citas/Edit/5
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

        // POST: Citas/Edit/5
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

        // GET: Citas/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var cita = await _citaService.ObtenerPorIdAsync(id.Value);
            if (cita == null) return NotFound();
            return View(cita);
        }

        // POST: Citas/Delete/5
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
                ViewBag.ListaBarberosCompleta = barberosSede;
            }
        }
    }
}