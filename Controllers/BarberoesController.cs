using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Models;
using SaaSBarberia.Services;
using System.Threading.Tasks;

public class BarberoesController : Controller
{
    private readonly BarberoService _barberoService;

    public BarberoesController(BarberoService barberoService)
    {
        _barberoService = barberoService;
    }

    // GET: Barberoes
    public async Task<IActionResult> Index()
    {
        return View(await _barberoService.ObtenerTodosConRelacionesAsync());
    }

    // GET: Barberoes/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var barbero = await _barberoService.ObtenerPorIdAsync(id.Value);
        if (barbero == null) return NotFound();

        return View(barbero);
    }

    // GET: Barberoes/Create
    public async Task<IActionResult> Create()
    {
        ViewBag.UsuarioId = new SelectList(await _barberoService.ObtenerUsuariosDisponiblesAsync(), "Id", "Nombre");
        ViewBag.TenantId = new SelectList(await _barberoService.ObtenerTenantsAsync(), "Id", "NombreNegocio");

        // Pasamos objeto inicializado por si acaso
        return View(new Barbero());
    }

    // POST: Barberoes/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,TenantId,UsuarioId,Especialidad,ComisionPorcentaje")] Barbero barbero)
    {
        // 🌟 CORRECCIÓN: Quitamos la validación de objetos virtuales
        ModelState.Remove(nameof(barbero.Tenant));
        ModelState.Remove(nameof(barbero.Usuario));

        if (ModelState.IsValid)
        {
            await _barberoService.GuardarAsync(barbero);
            return RedirectToAction(nameof(Index));
        }
        ViewBag.UsuarioId = new SelectList(await _barberoService.ObtenerUsuariosDisponiblesAsync(), "Id", "Nombre", barbero.UsuarioId);
        ViewBag.TenantId = new SelectList(await _barberoService.ObtenerTenantsAsync(), "Id", "NombreNegocio", barbero.TenantId);
        return View(barbero);
    }

    // GET: Barberoes/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var barbero = await _barberoService.ObtenerPorIdAsync(id.Value);
        if (barbero == null) return NotFound();

        ViewBag.UsuarioId = new SelectList(await _barberoService.ObtenerUsuariosDisponiblesAsync(), "Id", "Nombre", barbero.UsuarioId);
        ViewBag.TenantId = new SelectList(await _barberoService.ObtenerTenantsAsync(), "Id", "NombreNegocio", barbero.TenantId);
        return View(barbero);
    }

    // POST: Barberoes/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,TenantId,UsuarioId,Especialidad,ComisionPorcentaje")] Barbero barbero)
    {
        if (id != barbero.Id) return NotFound();

        // 🌟 CORRECCIÓN: Quitamos la validación de objetos virtuales en el Edit también
        ModelState.Remove(nameof(barbero.Tenant));
        ModelState.Remove(nameof(barbero.Usuario));

        if (ModelState.IsValid)
        {
            try
            {
                await _barberoService.GuardarAsync(barbero);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_barberoService.Existe(barbero.Id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }
        ViewBag.UsuarioId = new SelectList(await _barberoService.ObtenerUsuariosDisponiblesAsync(), "Id", "Nombre", barbero.UsuarioId);
        ViewBag.TenantId = new SelectList(await _barberoService.ObtenerTenantsAsync(), "Id", "NombreNegocio", barbero.TenantId);
        return View(barbero);
    }

    // GET: Barberoes/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var barbero = await _barberoService.ObtenerPorIdAsync(id.Value);
        if (barbero == null) return NotFound();

        return View(barbero);
    }

    // POST: Barberoes/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _barberoService.EliminarAsync(id);
        return RedirectToAction(nameof(Index));
    }
}