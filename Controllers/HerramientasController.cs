using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Models;
using SaaSBarberia.Services;

public class HerramientasController : Controller
{
    private readonly IHerramientasService _herramientasService;
    // Asumo que tienes una forma de obtener el Tenant logueado, cambia esto por tu lógica real
    private readonly int _currentTenantId;

    public HerramientasController(IHerramientasService herramientasService)
    {
        _herramientasService = herramientasService;
        // Simulación: Reemplaza esto por tu mecanismo real (ej. un ClaimsPrincipal o servicio de contexto)
        _currentTenantId = 1;
    }

    // GET: HERRAMIENTAS
    public async Task<IActionResult> Index()
    {
        var lista = await _herramientasService.GetAllAsync(_currentTenantId);
        return View(lista);
    }

    // GET: HERRAMIENTAS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var herramienta = await _herramientasService.GetByIdAsync(id.Value, _currentTenantId);
        if (herramienta == null) return NotFound();

        return View(herramienta);
    }

    // GET: HERRAMIENTAS/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: HERRAMIENTAS/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Nombre,Marca,NumeroSerie,Estado")] Herramienta herramienta)
    {
        if (ModelState.IsValid)
        {
            await _herramientasService.CreateAsync(herramienta, _currentTenantId);
            return RedirectToAction(nameof(Index));
        }
        return View(herramienta);
    }

    // GET: HERRAMIENTAS/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var herramienta = await _herramientasService.GetByIdAsync(id.Value, _currentTenantId);
        if (herramienta == null) return NotFound();

        return View(herramienta);
    }

    // POST: HERRAMIENTAS/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,Nombre,Marca,NumeroSerie,Estado")] Herramienta herramienta)
    {
        if (id != herramienta.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                await _herramientasService.UpdateAsync(herramienta, _currentTenantId);
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_herramientasService.Exists(herramienta.Id, _currentTenantId)) return NotFound();
                else throw;
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }
        return View(herramienta);
    }

    // GET: HERRAMIENTAS/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var herramienta = await _herramientasService.GetByIdAsync(id.Value, _currentTenantId);
        if (herramienta == null) return NotFound();

        return View(herramienta);
    }

    // POST: HERRAMIENTAS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _herramientasService.DeleteAsync(id, _currentTenantId);
        return RedirectToAction(nameof(Index));
    }
}