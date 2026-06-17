using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Models;
using SaaSBarberia.Services;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

public class CitasController : Controller
{
    private readonly CitaService _citaService;

    public CitasController(CitaService citaService)
    {
        _citaService = citaService;
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
        else if (User.IsInRole("Administrador") || User.IsInRole("Barbero"))
        {
            var empleado = await _citaService.ObtenerClientePorIdAsync(usuarioId);
            if (empleado != null && empleado.TenantId != null)
            {
                citas = citas.Where(c => c.TenantId == empleado.TenantId.Value).ToList();
            }
        }

        return View(citas);
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

        // 🧠 LÓGICA DE FILTRADO MULTI-TENANT POR ROL NIVEL DIOS
        if (User.IsInRole("SuperAdmin"))
        {
            // 😎 Eres tú, caremonda: Control total sobre el multiverso de barberías
            ViewBag.TenantId = new SelectList(await _citaService.ObtenerTenantsAsync(), "Id", "NombreNegocio");
            ViewBag.ClienteId = new SelectList(await _citaService.ObtenerClientesAsync(), "Id", "Nombre");

            await CargarCombosPorRolYTenantAsync(isSuperAdmin: true);
        }
        else
        {
            // Buscamos al usuario logueado (sea Cliente, Admin Local o Barbero) para amarrarle su Tenant
            var usuarioDb = await _citaService.ObtenerClientePorIdAsync(usuarioId);
            if (usuarioDb == null || usuarioDb.TenantId == null) return NotFound("Sede del usuario inválida o no asignada.");

            int tenantIdSede = usuarioDb.TenantId.Value;
            nuevaCita.TenantId = tenantIdSede;

            if (User.IsInRole("Cliente"))
            {
                // 🔒 CLIENTE: Auto-reserva bloqueada para él mismo
                nuevaCita.ClienteId = usuarioId;
            }
            else
            {
                // 🏢 ADMIN LOCAL / BARBERO: Pueden elegir cualquier cliente pero SOLO de su misma sede
                var clientesSede = (await _citaService.ObtenerClientesAsync()).Where(u => u.TenantId == tenantIdSede);
                ViewBag.ClienteId = new SelectList(clientesSede, "Id", "Nombre");
            }

            // Filtramos barberos y servicios exclusivamente para este TenantId corporativo
            await CargarCombosPorRolYTenantAsync(isSuperAdmin: false, tenantId: tenantIdSede);
        }

        return View(nuevaCita);
    }

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
            await _citaService.GuardarAsync(cita);
            return RedirectToAction(nameof(Index));
        }

        // RECARGA DE COMBOS DE SEGURIDAD EN CASO DE ERROR EN EL FORMULARIO
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

        return View(cita);
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
                if (!_citaService.Existe(cita.Id)) return NotFound();
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

    // 🔒 HELPER CENTRALIZADO DINÁMICO:
    private async Task CargarCombosPorRolYTenantAsync(bool isSuperAdmin, int tenantId = 0)
    {
        var barberos = await _citaService.ObtenerBarberosAsync();
        var servicios = await _citaService.ObtenerServiciosAsync();

        if (isSuperAdmin)
        {
            // Acceso total a nivel de infraestructura para el SuperAdmin
            ViewBag.BarberoId = new SelectList(barberos.Where(b => b.Usuario != null), "Id", "Usuario.Nombre");
            ViewBag.ServicioId = new SelectList(servicios, "Id", "Nombre");
        }
        else
        {
            // Candado estricto: Solo lo perteneciente a la sede local del usuario logueado
            var barberosSede = barberos.Where(b => b.TenantId == tenantId && b.Usuario != null);
            ViewBag.BarberoId = new SelectList(barberosSede, "Id", "Usuario.Nombre");

            var serviciosSede = servicios.Where(s => s.TenantId == tenantId);
            ViewBag.ServicioId = new SelectList(serviciosSede, "Id", "Nombre");
        }
    }
}