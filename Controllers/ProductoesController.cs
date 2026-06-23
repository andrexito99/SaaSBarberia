using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Models;
using SaaSBarberia.Services;

namespace SaaSBarberia.Controllers
{
    public class ProductosController : Controller
    {
        private readonly IProductosService _productosService;
        private readonly int _currentTenantId; // Reemplaza por tu lógica de sesión real

        public ProductosController(IProductosService productosService)
        {
            _productosService = productosService;
            _currentTenantId = 1; // ID simulado
        }

        // GET: Productos
        public async Task<IActionResult> Index()
        {
            var productos = await _productosService.GetAllAsync(_currentTenantId);
            return View(productos);
        }

        // GET: Productos/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var producto = await _productosService.GetByIdAsync(id.Value, _currentTenantId);
            if (producto == null) return NotFound();

            return View(producto);
        }

        // GET: Productos/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Productos/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Nombre,Descripcion,PrecioVenta,Stock")] Producto producto)
        {
            if (ModelState.IsValid)
            {
                await _productosService.CreateAsync(producto, _currentTenantId);
                return RedirectToAction(nameof(Index));
            }
            return View(producto);
        }

        // GET: Productos/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var producto = await _productosService.GetByIdAsync(id.Value, _currentTenantId);
            if (producto == null) return NotFound();

            return View(producto);
        }

        // POST: Productos/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int? id, [Bind("Id,Nombre,Descripcion,PrecioVenta,Stock")] Producto producto)
        {
            if (id != producto.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    await _productosService.UpdateAsync(producto, _currentTenantId);
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_productosService.Exists(producto.Id, _currentTenantId)) return NotFound();
                    else throw;
                }
                catch (UnauthorizedAccessException)
                {
                    return Forbid();
                }
            }
            return View(producto);
        }

        // GET: Productos/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var producto = await _productosService.GetByIdAsync(id.Value, _currentTenantId);
            if (producto == null) return NotFound();

            return View(producto);
        }

        // POST: Productos/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _productosService.DeleteAsync(id, _currentTenantId);
            return RedirectToAction(nameof(Index));
        }
    }
}