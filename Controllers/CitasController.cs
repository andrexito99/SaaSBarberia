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

public class CitasController : Controller
{
    private readonly CitaService _citaService;
    // ⚠️ ATENCIÓN: Inyectamos tu contexto de base de datos para mapear las colisiones en el nuevo método
    private readonly BarberSaaSDbContext _context;

    public CitasController(CitaService citaService, BarberSaaSDbContext context)
    {
        _citaService = citaService;
        _context = context;
    }

    // GET: Citas
    public async Task<IActionResult> Index()
    {
        var usuarioIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(usuarioIdStr)) return RedirectToAction("Login", "Account");
        int usuarioId = int.Parse(usuarioIdStr);

        var citas = await _citaService.ObtenerTodasConRelacionesAsync();

        // Si es cliente, solo ve sus propias citas asignadas
        if (User.IsInRole("Cliente"))
        {
            citas = citas.Where(c => c.ClienteId == usuarioId).ToList();
        }
        // Si es Admin local o Barbero, solo ve las citas de SU sede corporativa
        else if (User.IsInRole("AdminBarberia") || User.IsInRole("Barbero"))
        {
            var empleado = await _citaService.ObtenerClientePorIdAsync(usuarioId);
            if (empleado != null && empleado.TenantId != null)
            {
                citas = citas.Where(c => c.TenantId == empleado.TenantId.Value).ToList();
            }
        }

        return View(citas);
    }

    // ⚡ ENDPOINT API: Calcula los bloques libres calculando la duración del servicio
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

        // 1. Obtener la duración del servicio solicitado
        var servicio = await _context.Servicios.FindAsync(servicioId);
        if (servicio == null) return NotFound("Servicio no encontrado.");
        int duracionNuevaCita = servicio.DuracionMinutos;

        // 2. Definir jornada base por defecto (8:00 AM a 7:00 PM)
        DateTime horaInicioJornada = fechaSeleccionada.Date.AddHours(8).AddMinutes(0);
        DateTime horaFinJornada = fechaSeleccionada.Date.AddHours(19).AddMinutes(0);

        // 3. Traer las citas que ya tiene ese barbero para ESE DÍA (omitir canceladas)
        var citasExistentes = await _context.Citas
            .Where(c => c.BarberoId == barberoId
                     && c.FechaHora.Date == fechaSeleccionada.Date
                     && c.Estado != "Cancelada")
            .Include(c => c.Servicio)
            .OrderBy(c => c.FechaHora)
            .ToListAsync();

        List<string> horariosDisponibles = new List<string>();

        // 4. Generar intervalos dinámicos cada 20 minutos
        int intervaloBloques = 20;
        DateTime bloqueEvaluar = horaInicioJornada;

        while (bloqueEvaluar.AddMinutes(duracionNuevaCita) <= horaFinJornada)
        {
            DateTime inicioNuevo = bloqueEvaluar;
            DateTime finNuevo = bloqueEvaluar.AddMinutes(duracionNuevaCita);
            bool cruza = false;

            // Fórmula matemática de colisión de intervalos
            foreach (var cita in citasExistentes)
            {
                DateTime inicioExistente = cita.FechaHora;
                DateTime finExistente = cita.FechaHora.AddMinutes(cita.Servicio.DuracionMinutos);

                if (inicioNuevo < finExistente && finNuevo > inicioExistente)
                {
                    cruza = true;
                    break;
                }
            }

            // Si el bloque está limpio, se añade a la lista en formato legible
            if (!cruza)
            {
                horariosDisponibles.Add(inicioNuevo.ToString("hh:mm tt"));
            }

            bloqueEvaluar = bloqueEvaluar.AddMinutes(intervaloBloques);
        }

        return Json(horariosDisponibles);
    }

    // POST: Citas/Cancelar/5
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(int id)
    {
        var cita = await _citaService.ObtenerPorIdAsync(id);
        if (cita == null) return NotFound();

        var usuarioIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (User.IsInRole("Cliente") && cita.ClienteId != int.Parse(usuarioIdStr))
        {
            return Forbid();
        }

        cita.Estado = "Cancelada";
        await _citaService.GuardarAsync(cita);

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
    [Authorize]
    public async Task<IActionResult> Create()
    {
        var usuarioIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(usuarioIdStr)) return RedirectToAction("Login", "Account");

        int usuarioId = int.Parse(usuarioIdStr);
        var nuevaCita = new Cita { FechaHora = DateTime.Now, Estado = "Pendiente" };

        if (User.IsInRole("SuperAdmin"))
        {
            ViewBag.TenantId = new SelectList(await _citaService.ObtenerTenantsAsync(), "Id", "NombreNegocio");
            ViewBag.ClienteId = new SelectList(await _citaService.ObtenerClientesAsync(), "Id", "Nombre");

            await CargarCombosPorRolYTenantAsync(isSuperAdmin: true);
        }
        else
        {
            var usuarioDb = await _citaService.ObtenerClientePorIdAsync(usuarioId);
            if (usuarioDb == null || usuarioDb.TenantId == null) return NotFound("Sede del usuario inválida o no asignada.");

            int tenantIdSede = usuarioDb.TenantId.Value;
            nuevaCita.TenantId = tenantIdSede;

            if (User.IsInRole("Cliente"))
            {
                nuevaCita.ClienteId = usuarioId;
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

    // POST: Citas/Create
    // POST: Citas/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,TenantId,ClienteId,BarberoId,ServicioId,FechaHora,Estado,Notas")] Cita cita)
    {
        ModelState.Remove(nameof(cita.Tenant));
        ModelState.Remove(nameof(cita.Cliente));
        ModelState.Remove(nameof(cita.Barbero));
        ModelState.Remove(nameof(cita.Servicio));

        if (ModelState.IsValid)
        {
            // 1. Obtener la duración del servicio actual para calcular el rango de tiempo
            var servicio = await _context.Servicios.FindAsync(cita.ServicioId);
            if (servicio == null) return NotFound("El servicio seleccionado no existe.");

            DateTime inicioNuevaCita = cita.FechaHora;
            DateTime finNuevaCita = cita.FechaHora.AddMinutes(servicio.DuracionMinutos);

            // 2. CONTROL 1: Validar que el BARBERO no se pise con otra cita activa de la sede
            var citasBarbero = await _context.Citas
                .Where(c => c.BarberoId == cita.BarberoId
                         && c.FechaHora.Date == cita.FechaHora.Date
                         && c.Estado != "Cancelada"
                         && c.Id != cita.Id)
                .Include(c => c.Servicio)
                .ToListAsync();

            foreach (var c in citasBarbero)
            {
                DateTime inicioExistente = c.FechaHora;
                DateTime finExistente = c.FechaHora.AddMinutes(c.Servicio.DuracionMinutos);

                if (inicioNuevaCita < finExistente && finNuevaCita > inicioExistente)
                {
                    ModelState.AddModelError("", "Lo sentimos, el barbero ya se encuentra ocupado en ese horario. ¡Te ganaron el turno por un segundo!");
                    await RecargarCombosErrorAsync(cita);
                    return View(cita);
                }
            }

            // 3. CONTROL 2: Validar que el CLIENTE no tenga otra cita activa a esa misma hora (Parche de tu captura)
            var citasCliente = await _context.Citas
                .Where(c => c.ClienteId == cita.ClienteId
                         && c.FechaHora.Date == cita.FechaHora.Date
                         && c.Estado != "Cancelada"
                         && c.Id != cita.Id)
                .Include(c => c.Servicio)
                .ToListAsync();

            foreach (var c in citasCliente)
            {
                DateTime inicioExistente = c.FechaHora;
                DateTime finExistente = c.FechaHora.AddMinutes(c.Servicio.DuracionMinutos);

                if (inicioNuevaCita < finExistente && finNuevaCita > inicioExistente)
                {
                    ModelState.AddModelError("", "¡Pilas! Ya tienes otra cita agendada que se cruza con este horario.");
                    await RecargarCombosErrorAsync(cita);
                    return View(cita);
                }
            }

            // Si pasa ambos filtros con éxito, se guarda limpio
            await _citaService.GuardarAsync(cita);
            return RedirectToAction(nameof(Index));
        }

        await RecargarCombosErrorAsync(cita);
        return View(cita);
    }

    // 📦 Método auxiliar privado para no repetir código de recarga de combos en caso de error
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
                await _citaService.GuardarAsync(cita);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Citas.Any(e => e.Id == cita.Id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }

        ViewBag.TenantId = new SelectList(await _citaService.ObtenerTenantsAsync(), "Id", "NombreNegocio", cita.TenantId);
        ViewBag.ClienteId = new SelectList(await _citaService.ObtenerClientesAsync(), "Id", "Nombre", cita.ClienteId);
        var barberos = await _citaService.ObtenerBarberosAsync();
        ViewBag.BarberoId = new SelectList(barberos, "Id", "Usuario.Nombre", cita.BarberoId);
        ViewBag.ServicioId = new SelectList(await _citaService.ObtenerServiciosAsync(), "Id", "Nombre", cita.ServicioId);
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
        await _citaService.EliminarAsync(id);
        return RedirectToAction(nameof(Index));
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
            ViewBag.ListaBarberosCompleta = barberosValidos;
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