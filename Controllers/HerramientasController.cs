using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data; // Asegúrate de que apunte a tu namespace de Data
using SaaSBarberia.Models;
using SaaSBarberia.Services;

namespace SaaSBarberia.Controllers
{
    [Authorize(Roles = "AdminBarberia,SuperAdmin")] // 🚨 Seguridad activada
    public class HerramientasController : Controller
    {
        private readonly IHerramientasService _herramientasService;
        private readonly BarberSaaSDbContext _context; // 🌟 Inyectamos el contexto para leer la sesión

        public HerramientasController(IHerramientasService herramientasService, BarberSaaSDbContext context)
        {
            _herramientasService = herramientasService;
            _context = context;
        }

        // 🔑 PROPIEDAD DINÁMICA: Extrae el Tenant real del usuario en sesión
        private int CurrentTenantId
        {
            get
            {
                return _context.TenantIdActual ?? throw new UnauthorizedAccessException("Papi, el Tenant no existe o no estás logueado.");
            }
        }

        // GET: HERRAMIENTAS
        public async Task<IActionResult> Index()
        {
            var lista = await _herramientasService.GetAllAsync(CurrentTenantId);
            return View(lista);
        }

        // GET: HERRAMIENTAS/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var herramienta = await _herramientasService.GetByIdAsync(id.Value, CurrentTenantId);
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
                await _herramientasService.CreateAsync(herramienta, CurrentTenantId);
                return RedirectToAction(nameof(Index));
            }
            return View(herramienta);
        }

        // GET: HERRAMIENTAS/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var herramienta = await _herramientasService.GetByIdAsync(id.Value, CurrentTenantId);
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

        // GET: HERRAMIENTAS/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var herramienta = await _herramientasService.GetByIdAsync(id.Value, CurrentTenantId);
            if (herramienta == null) return NotFound();

            return View(herramienta);
        }

        // POST: HERRAMIENTAS/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _herramientasService.DeleteAsync(id, CurrentTenantId);
            return RedirectToAction(nameof(Index));
        }
    }
}