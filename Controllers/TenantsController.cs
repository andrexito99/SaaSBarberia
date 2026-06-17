using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Models;
using SaaSBarberia.Services;
using System.Threading.Tasks;

public class TenantsController : Controller
{
    private readonly TenantService _tenantService;

    // Inyectamos el servicio purito
    public TenantsController(TenantService tenantService)
    {
        _tenantService = tenantService;
    }

    // GET: Tenants
    public async Task<IActionResult> Index()
    {
        var lista = await _tenantService.ObtenerTodosAsync();
        return View(lista);
    }

    // GET: Tenants/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var tenant = await _tenantService.ObtenerPorIdAsync(id.Value);
        if (tenant == null) return NotFound();

        return View(tenant);
    }

    // GET: Tenants/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: Tenants/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,NombreNegocio,Subdominio,PlanSuscripcion,Activo,FechaRegistro")] Tenant tenant)
    {
        if (ModelState.IsValid)
        {
            await _tenantService.GuardarAsync(tenant);
            return RedirectToAction(nameof(Index));
        }
        return View(tenant);
    }

    // GET: Tenants/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var tenant = await _tenantService.ObtenerPorIdAsync(id.Value);
        if (tenant == null) return NotFound();

        return View(tenant);
    }

    // POST: Tenants/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,NombreNegocio,Subdominio,PlanSuscripcion,Activo,FechaRegistro")] Tenant tenant)
    {
        if (id != tenant.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                await _tenantService.GuardarAsync(tenant);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_tenantService.Existe(tenant.Id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(tenant);
    }

    // GET: Tenants/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var tenant = await _tenantService.ObtenerPorIdAsync(id.Value);
        if (tenant == null) return NotFound();

        return View(tenant);
    }

    // POST: Tenants/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _tenantService.EliminarAsync(id);
        return RedirectToAction(nameof(Index));
    }
}