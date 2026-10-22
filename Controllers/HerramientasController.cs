using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;
using SaaSBarberia.Services;

namespace SaaSBarberia.Controllers
{
    [Authorize(Roles = "Administrador,SuperAdmin")]
    public class HerramientasController : Controller
    {
        private readonly IHerramientasService _herramientasService;
        private readonly BarberSaaSDbContext _context;

        public HerramientasController(IHerramientasService herramientasService, BarberSaaSDbContext context)
        {
            _herramientasService = herramientasService;
            _context = context;
        }

        // 🔑 PROPIEDAD DINÁMICA: Extrae el Tenant real del usuario usando el DbContext inteligente
        private int CurrentTenantId
        {
            get
            {
                return _context.TenantIdActual ?? throw new UnauthorizedAccessException("El Tenant no existe o la sesión no es válida.");
            }
        }

        // 🔒 CERROJO DE SEGURIDAD MULTI-TENANT MODULAR
        private async Task<bool> ValidarAccesoModulo()
        {
            if (User.IsInRole("SuperAdmin")) return true;

            var tenant = await _context.Tenants.FindAsync(CurrentTenantId);
            return tenant != null && tenant.TieneModuloHerramientas;
        }

        // GET: Herramientas
        public async Task<IActionResult> Index()
        {
            if (!await ValidarAccesoModulo()) return Forbid();

            var lista = await _herramientasService.GetAllAsync(CurrentTenantId);
            return View(lista);
        }

        // GET: Herramientas/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (!await ValidarAccesoModulo()) return Forbid();
            if (id == null) return NotFound();

            var herramienta = await _herramientasService.GetByIdAsync(id.Value, CurrentTenantId);
            if (herramienta == null) return NotFound();

            return View(herramienta);
        }

        // GET: Herramientas/Create
        public async Task<IActionResult> Create()
        {
            if (!await ValidarAccesoModulo()) return Forbid();

            return View();
        }

        // POST: Herramientas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Nombre,Marca,NumeroSerie,Estado")] Herramienta herramienta)
        {
            if (!await ValidarAccesoModulo()) return Forbid();

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
            if (!await ValidarAccesoModulo()) return Forbid();
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
            if (!await ValidarAccesoModulo()) return Forbid();
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

        // POST: Herramientas/CambiarEstado/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(int id, string nuevoEstado)
        {
            if (!await ValidarAccesoModulo()) return Forbid();

            var herramienta = await _herramientasService.GetByIdAsync(id, CurrentTenantId);
            if (herramienta == null) return NotFound();

            var estadosValidos = new[] { "Disponible", "Alquilado", "Mantenimiento", "Dañado" };
            if (estadosValidos.Contains(nuevoEstado))
            {
                herramienta.Estado = nuevoEstado;
                await _herramientasService.UpdateAsync(herramienta, CurrentTenantId);
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Herramientas/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (!await ValidarAccesoModulo()) return Forbid();
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
            if (!await ValidarAccesoModulo()) return Forbid();

            await _herramientasService.DeleteAsync(id, CurrentTenantId);
            return RedirectToAction(nameof(Index));
        }
    }
}