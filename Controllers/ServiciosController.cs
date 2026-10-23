using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Models;
using SaaSBarberia.Services;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

[Authorize]
public class ServiciosController : Controller
{
    private readonly ServicioService _servicioService;
    private readonly CitaService _citaService;

    public ServiciosController(ServicioService servicioService, CitaService citaService)
    {
        _servicioService = servicioService;
        _citaService = citaService;
    }

    // GET: Servicios (Permitido para Clientes y personal autorizado)
    public async Task<IActionResult> Index()
    {
        var usuarioIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (User.Identity != null && User.Identity.IsAuthenticated && !string.IsNullOrEmpty(usuarioIdStr))
        {
            int usuarioId = int.Parse(usuarioIdStr);
            var usuarioDb = await _citaService.ObtenerClientePorIdAsync(usuarioId);

            if (usuarioDb != null && usuarioDb.TenantId != null)
            {
                if (User.IsInRole("Cliente") || User.IsInRole("Administrador") || User.IsInRole("Barbero"))
                {
                    var serviciosFiltrados = await _servicioService.ObtenerTodosPorTenantAsync(usuarioDb.TenantId.Value);
                    return View(serviciosFiltrados);
                }
            }
        }

        return View(await _servicioService.ObtenerTodosConTenantAsync());
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var servicio = await _servicioService.ObtenerPorIdAsync(id.Value);
        if (servicio == null) return NotFound();
        return View(servicio);
    }

    [Authorize(Roles = "Administrador,SuperAdmin")]
    public async Task<IActionResult> Create()
    {
        var usuarioIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        int usuarioId = int.Parse(usuarioIdStr ?? "0");
        var tenants = await _servicioService.ObtenerTenantsAsync();

        if (User.IsInRole("Administrador"))
        {
            var adminDb = await _citaService.ObtenerClientePorIdAsync(usuarioId);
            if (adminDb != null && adminDb.TenantId != null)
            {
                ViewBag.TenantId = new SelectList(tenants.Where(t => t.Id == adminDb.TenantId.Value), "Id", "NombreNegocio", adminDb.TenantId.Value);
            }
        }
        else
        {
            ViewBag.TenantId = new SelectList(tenants, "Id", "NombreNegocio");
        }

        return View(new Servicio());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,SuperAdmin")]
    public async Task<IActionResult> Create([Bind("Id,TenantId,Nombre,Precio,DuracionMinutos")] Servicio servicio)
    {
        ModelState.Remove(nameof(servicio.Tenant));

        if (ModelState.IsValid)
        {
            await _servicioService.GuardarAsync(servicio);
            return RedirectToAction(nameof(Index));
        }

        var tenants = await _servicioService.ObtenerTenantsAsync();
        ViewBag.TenantId = new SelectList(tenants, "Id", "NombreNegocio", servicio.TenantId);
        return View(servicio);
    }

    [Authorize(Roles = "Administrador,SuperAdmin")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var servicio = await _servicioService.ObtenerPorIdAsync(id.Value);
        if (servicio == null) return NotFound();

        var tenants = await _servicioService.ObtenerTenantsAsync();
        ViewBag.TenantId = new SelectList(tenants, "Id", "NombreNegocio", servicio.TenantId);
        return View(servicio);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,SuperAdmin")]
    public async Task<IActionResult> Edit(int id, [Bind("Id,TenantId,Nombre,Precio,DuracionMinutos")] Servicio servicio)
    {
        if (id != servicio.Id) return NotFound();
        ModelState.Remove(nameof(servicio.Tenant));

        if (ModelState.IsValid)
        {
            try
            {
                await _servicioService.GuardarAsync(servicio);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_servicioService.Existe(servicio.Id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }

        var tenants = await _servicioService.ObtenerTenantsAsync();
        ViewBag.TenantId = new SelectList(tenants, "Id", "NombreNegocio", servicio.TenantId);
        return View(servicio);
    }

    [Authorize(Roles = "Administrador,SuperAdmin")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var servicio = await _servicioService.ObtenerPorIdAsync(id.Value);
        if (servicio == null) return NotFound();
        return View(servicio);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,SuperAdmin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _servicioService.EliminarAsync(id);
        return RedirectToAction(nameof(Index));
    }
}