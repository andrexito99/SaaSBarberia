using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Models;
using SaaSBarberia.Services;

public class AlquileresHerramientasController : Controller
{
    private readonly IAlquileresHerramientasService _alquileresService;
    private readonly int _currentTenantId;

    public AlquileresHerramientasController(IAlquileresHerramientasService alquileresService)
    {
        _alquileresService = alquileresService;
        _currentTenantId = 1; // ID simulado
    }

    // GET: AlquileresHerramientas
    public async Task<IActionResult> Index()
    {
        var lista = await _alquileresService.GetAllAsync(_currentTenantId);
        return View(lista);
    }

    // GET: AlquileresHerramientas/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var alquiler = await _alquileresService.GetByIdAsync(id.Value, _currentTenantId);
        if (alquiler == null) return NotFound();

        return View(alquiler);
    }

    // GET: AlquileresHerramientas/Create
    public async Task<IActionResult> Create()
    {
        var herramientas = await _alquileresService.GetHerramientasDisponiblesAsync(_currentTenantId);
        var barberos = await _alquileresService.GetBarberosAsync(_currentTenantId);

        ViewData["HerramientaId"] = new SelectList(herramientas, "Id", "Nombre");
        ViewData["BarberoId"] = new SelectList(barberos, "Id", "Usuario.Nombre"); // 👈 Solucionado aquí
        return View();
    }

    // POST: AlquileresHerramientas/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,HerramientaId,BarberoId,FechaPrestamo,FechaDevolucion,CostoAlquiler")] AlquilerHerramienta alquiler)
    {
        if (ModelState.IsValid)
        {
            await _alquileresService.CreateAsync(alquiler, _currentTenantId);
            return RedirectToAction(nameof(Index));
        }

        ViewData["HerramientaId"] = new SelectList(await _alquileresService.GetHerramientasDisponiblesAsync(_currentTenantId), "Id", "Nombre", alquiler.HerramientaId);
        ViewData["BarberoId"] = new SelectList(await _alquileresService.GetBarberosAsync(_currentTenantId), "Id", "Usuario.Nombre", alquiler.BarberoId);
        return View(alquiler);
    }

    // GET: AlquileresHerramientas/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var alquiler = await _alquileresService.GetByIdAsync(id.Value, _currentTenantId);
        if (alquiler == null) return NotFound();

        ViewData["HerramientaId"] = new SelectList(await _alquileresService.GetHerramientasDisponiblesAsync(_currentTenantId, alquiler.HerramientaId), "Id", "Nombre", alquiler.HerramientaId);
        ViewData["BarberoId"] = new SelectList(await _alquileresService.GetBarberosAsync(_currentTenantId), "Id", "Usuario.Nombre", alquiler.BarberoId);
        return View(alquiler);
    }

    // POST: AlquileresHerramientas/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,HerramientaId,BarberoId,FechaPrestamo,FechaDevolucion,CostoAlquiler")] AlquilerHerramienta alquiler)
    {
        if (id != alquiler.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                await _alquileresService.UpdateAsync(alquiler, _currentTenantId);
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_alquileresService.Exists(alquiler.Id, _currentTenantId)) return NotFound();
                else throw;
            }
        }
        ViewData["HerramientaId"] = new SelectList(await _alquileresService.GetHerramientasDisponiblesAsync(_currentTenantId, alquiler.HerramientaId), "Id", "Nombre", alquiler.HerramientaId);
        ViewData["BarberoId"] = new SelectList(await _alquileresService.GetBarberosAsync(_currentTenantId), "Id", "Usuario.Nombre", alquiler.BarberoId);
        return View(alquiler);
    }

    // GET: AlquileresHerramientas/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var alquiler = await _alquileresService.GetByIdAsync(id.Value, _currentTenantId);
        if (alquiler == null) return NotFound();

        return View(alquiler);
    }
        
    // POST: AlquileresHerramientas/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _alquileresService.DeleteAsync(id, _currentTenantId);
        return RedirectToAction(nameof(Index));
    }
}