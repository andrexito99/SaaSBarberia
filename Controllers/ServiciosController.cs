using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Models;
using SaaSBarberia.Services;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

public class ServiciosController : Controller
{
    private readonly ServicioService _servicioService;
    private readonly CitaService _citaService; // 🌟 LA CLAVE: Inyectamos el servicio de citas para buscar el TenantId del usuario

    public ServiciosController(ServicioService servicioService, CitaService citaService)
    {
        _servicioService = servicioService;
        _citaService = citaService;
    }

    // GET: Servicios
    public async Task<IActionResult> Index()
    {
        // 1. Si es un Cliente logueado, le capamos el catálogo para que SOLO vea su barbería asociada
        if (User.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("Cliente"))
        {
            var usuarioIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(usuarioIdStr))
            {
                int usuarioId = int.Parse(usuarioIdStr);

                // Buscamos al cliente en la BD para saber a qué maldito Tenant pertenece real
                var clienteDb = await _citaService.ObtenerClientePorIdAsync(usuarioId);
                if (clienteDb != null && clienteDb.TenantId != null)
                {
                    // 🔒 FILTRADO HERMÉTICO: Solo cargamos los servicios de SU sede asociada
                    var serviciosFiltrados = await _servicioService.ObtenerTodosPorTenantAsync(clienteDb.TenantId.Value);
                    return View(serviciosFiltrados);
                }
            }
        }

        // 2. Si es SuperAdmin, Administrador o un usuario sin loguear (público), ve todo el catálogo global
        return View(await _servicioService.ObtenerTodosConTenantAsync());
    }

    // GET: Servicios/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var servicio = await _servicioService.ObtenerPorIdAsync(id.Value);
        if (servicio == null) return NotFound();

        return View(servicio);
    }

    // GET: Servicios/Create
    public async Task<IActionResult> Create()
    {
        ViewBag.TenantId = new SelectList(await _servicioService.ObtenerTenantsAsync(), "Id", "NombreNegocio");
        return View(new Servicio());
    }

    // POST: Servicios/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,TenantId,Nombre,Precio,DuracionMinutos")] Servicio servicio)
    {
        ModelState.Remove(nameof(servicio.Tenant));

        if (ModelState.IsValid)
        {
            await _servicioService.GuardarAsync(servicio);
            return RedirectToAction(nameof(Index));
        }
        ViewBag.TenantId = new SelectList(await _servicioService.ObtenerTenantsAsync(), "Id", "NombreNegocio", servicio.TenantId);
        return View(servicio);
    }

    // GET: Servicios/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var servicio = await _servicioService.ObtenerPorIdAsync(id.Value);
        if (servicio == null) return NotFound();

        ViewBag.TenantId = new SelectList(await _servicioService.ObtenerTenantsAsync(), "Id", "NombreNegocio", servicio.TenantId);
        return View(servicio);
    }

    // POST: Servicios/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
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
        ViewBag.TenantId = new SelectList(await _servicioService.ObtenerTenantsAsync(), "Id", "NombreNegocio", servicio.TenantId);
        return View(servicio);
    }

    // GET: Servicios/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var servicio = await _servicioService.ObtenerPorIdAsync(id.Value);
        if (servicio == null) return NotFound();

        return View(servicio);
    }

    // POST: Servicios/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _servicioService.EliminarAsync(id);
        return RedirectToAction(nameof(Index));
    }
}