using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;
using SaaSBarberia.Services;

[Authorize(Roles = "AdminBarberia,SuperAdmin")]
public class AlquileresHerramientasController : Controller
{
    private readonly IAlquileresHerramientasService _alquileresService;
    private readonly BarberSaaSDbContext _context;

    public AlquileresHerramientasController(IAlquileresHerramientasService alquileresService, BarberSaaSDbContext context)
    {
        _alquileresService = alquileresService;
        _context = context;
    }

    private int CurrentTenantId
    {
        get
        {
            return _context.TenantIdActual ?? throw new UnauthorizedAccessException("Papi, el Tenant no existe o no estás logueado.");
        }
    }

    // 🔒 CERROJO DE SEGURIDAD MULTI-TENANT MODULAR
    private async Task<bool> ValidarAccesoModulo()
    {
        if (User.IsInRole("SuperAdmin")) return true;

        var tenant = await _context.Tenants.FindAsync(CurrentTenantId);
        return tenant != null && tenant.TieneModuloHerramientas;
    }

    // GET: AlquileresHerramientas
    public async Task<IActionResult> Index()
    {
        if (!await ValidarAccesoModulo()) return Forbid();

        var lista = await _alquileresService.GetAllAsync(CurrentTenantId);
        return View(lista);
    }

    // GET: AlquileresHerramientas/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (!await ValidarAccesoModulo()) return Forbid();
        if (id == null) return NotFound();

        var alquiler = await _alquileresService.GetByIdAsync(id.Value, CurrentTenantId);
        if (alquiler == null) return NotFound();

        return View(alquiler);
    }

    // GET: AlquileresHerramientas/Create
    public async Task<IActionResult> Create()
    {
        if (!await ValidarAccesoModulo()) return Forbid();

        var herramientas = await _alquileresService.GetHerramientasDisponiblesAsync(CurrentTenantId);
        var barberos = await _alquileresService.GetBarberosAsync(CurrentTenantId);

        ViewData["HerramientaId"] = new SelectList(herramientas, "Id", "Nombre");

        var barberosSelect = barberos.Select(b => new {
            Id = b.Id,
            NombreCompleto = b.Usuario != null ? b.Usuario.Nombre : "Barbero Sin Nombre"
        });
        ViewData["BarberoId"] = new SelectList(barberosSelect, "Id", "NombreCompleto");

        return View();
    }

    // POST: AlquileresHerramientas/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,HerramientaId,BarberoId,FechaPrestamo,FechaDevolucion,CostoAlquiler")] AlquilerHerramienta alquiler)
    {
        if (!await ValidarAccesoModulo()) return Forbid();

        if (ModelState.IsValid)
        {
            await _alquileresService.CreateAsync(alquiler, CurrentTenantId);
            return RedirectToAction(nameof(Index));
        }

        var herramientas = await _alquileresService.GetHerramientasDisponiblesAsync(CurrentTenantId);
        var barberos = await _alquileresService.GetBarberosAsync(CurrentTenantId);

        ViewData["HerramientaId"] = new SelectList(herramientas, "Id", "Nombre", alquiler.HerramientaId);

        var barberosSelect = barberos.Select(b => new {
            Id = b.Id,
            NombreCompleto = b.Usuario != null ? b.Usuario.Nombre : "Barbero Sin Nombre"
        });
        ViewData["BarberoId"] = new SelectList(barberosSelect, "Id", "NombreCompleto", alquiler.BarberoId);

        return View(alquiler);
    }

    // GET: AlquileresHerramientas/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (!await ValidarAccesoModulo()) return Forbid();
        if (id == null) return NotFound();

        var alquiler = await _alquileresService.GetByIdAsync(id.Value, CurrentTenantId);
        if (alquiler == null) return NotFound();

        var herramientas = await _alquileresService.GetHerramientasDisponiblesAsync(CurrentTenantId, alquiler.HerramientaId);
        var barberos = await _alquileresService.GetBarberosAsync(CurrentTenantId);

        ViewData["HerramientaId"] = new SelectList(herramientas, "Id", "Nombre", alquiler.HerramientaId);

        var barberosSelect = barberos.Select(b => new {
            Id = b.Id,
            NombreCompleto = b.Usuario != null ? b.Usuario.Nombre : "Barbero Sin Nombre"
        });
        ViewData["BarberoId"] = new SelectList(barberosSelect, "Id", "NombreCompleto", alquiler.BarberoId);

        return View(alquiler);
    }

    // POST: AlquileresHerramientas/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,HerramientaId,BarberoId,FechaPrestamo,FechaDevolucion,CostoAlquiler")] AlquilerHerramienta alquiler)
    {
        if (!await ValidarAccesoModulo()) return Forbid();
        if (id != alquiler.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                await _alquileresService.UpdateAsync(alquiler, CurrentTenantId);
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_alquileresService.Exists(alquiler.Id, CurrentTenantId)) return NotFound();
                else throw;
            }
        }

        var herramientas = await _alquileresService.GetHerramientasDisponiblesAsync(CurrentTenantId, alquiler.HerramientaId);
        var barberos = await _alquileresService.GetBarberosAsync(CurrentTenantId);

        ViewData["HerramientaId"] = new SelectList(herramientas, "Id", "Nombre", alquiler.HerramientaId);

        var barberosSelect = barberos.Select(b => new {
            Id = b.Id,
            NombreCompleto = b.Usuario != null ? b.Usuario.Nombre : "Barbero Sin Nombre"
        });
        ViewData["BarberoId"] = new SelectList(barberosSelect, "Id", "NombreCompleto", alquiler.BarberoId);

        return View(alquiler);
    }

    // GET: AlquileresHerramientas/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (!await ValidarAccesoModulo()) return Forbid();
        if (id == null) return NotFound();

        var alquiler = await _alquileresService.GetByIdAsync(id.Value, CurrentTenantId);
        if (alquiler == null) return NotFound();

        return View(alquiler);
    }

    // POST: AlquileresHerramientas/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (!await ValidarAccesoModulo()) return Forbid();

        await _alquileresService.DeleteAsync(id, CurrentTenantId);
        return RedirectToAction(nameof(Index));
    }
}