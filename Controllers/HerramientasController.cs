using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;
using SaaSBarberia.Services;

namespace SaaSBarberia.Controllers
{
    [Authorize(Roles = "Administrador,SuperAdmin")] // 🚨 Seguridad activada full
    public class HerramientasController : Controller
    {
        private readonly IHerramientasService _herramientasService;
        private readonly BarberSaaSDbContext _context; // 🌟 Inyectamos el contexto para leer la sesión distribuida

        public HerramientasController(IHerramientasService herramientasService, BarberSaaSDbContext context)
        {
            _herramientasService = herramientasService;
            _context = context;
        }

        // 🔑 PROPIEDAD DINÁMICA: Extrae el Tenant real del usuario usando tu DbContext inteligente
        private int CurrentTenantId
        {
            get
            {
                return _context.TenantIdActual ?? throw new UnauthorizedAccessException("Papi, el Tenant no existe o no estás logueado.");
            }
        }

        // GET: Herramientas
        public async Task<IActionResult> Index()
        {
            var lista = await _herramientasService.GetAllAsync(CurrentTenantId);
            return View(lista);
        }

        // GET: Herramientas/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var herramienta = await _herramientasService.GetByIdAsync(id.Value, CurrentTenantId);
            if (herramienta == null) return NotFound();

            return View(herramienta);
        }

        // GET: Herramientas/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Herramientas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Nombre,Marca,NumeroSerie,Estado")] Herramienta herramienta)
        {
            if (ModelState.IsValid)
            {
                await _herramientasService.CreateAsync(herramienta, CurrentTenantId);
                return RedirectToAction(nameof(Index));
            }
            return View(herramienta);
        }

        // GET: Herramientas/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var herramienta = await _herramientasService.GetByIdAsync(id.Value, CurrentTenantId);
            if (herramienta == null) return NotFound();

            return View(herramienta);
        }

        // POST: Herramientas/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int? id, [Bind("Id,Nombre,Marca,NumeroSerie,Estado")] Herramienta herramienta)
        {
            if (id != herramienta.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    await _herramientasService.UpdateAsync(herramienta, CurrentTenantId);
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_herramientasService.Exists(herramienta.Id, CurrentTenantId)) return NotFound();
                    else throw;
                }
                catch (UnauthorizedAccessException)
                {
                    return Forbid();
                }
            }
            return View(herramienta);
        }

        // GET: Herramientas/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var herramienta = await _herramientasService.GetByIdAsync(id.Value, CurrentTenantId);
            if (herramienta == null) return NotFound();

            return View(herramienta);
        }

        // POST: Herramientas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _herramientasService.DeleteAsync(id, CurrentTenantId);
            return RedirectToAction(nameof(Index));
        }
    }
}