using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Filters;
using SaaSBarberia.Models;
using SaaSBarberia.Services;

namespace SaaSBarberia.Controllers
{
    [Authorize(Roles = "Administrador,SuperAdmin")]
    [TenantAuthorize]
    public class ProductosController : Controller
    {
        private readonly IProductosService _productosService;
        private readonly CloudinaryStorageService _storageService;

        public ProductosController(IProductosService productosService, CloudinaryStorageService storageService)
        {
            _productosService = productosService;
            _storageService   = storageService;
        }

        // ── Helpers ─────────────────────────────────────────────────────────────────

        private int CurrentTenantId
        {
            get
            {
                var val = User.FindFirst("TenantId")?.Value;
                if (string.IsNullOrEmpty(val) || !int.TryParse(val, out int id))
                    throw new UnauthorizedAccessException("No se pudo identificar el Tenant.");
                return id;
            }
        }

        private int    CurrentUsuarioId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int uid) ? uid : 0;
        private string CurrentUserIp    => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        // ── Index ────────────────────────────────────────────────────────────────────

        public async Task<IActionResult> Index()
        {
            var productos = await _productosService.GetAllAsync(CurrentTenantId);
            return View(productos);
        }

        // ── Details ──────────────────────────────────────────────────────────────────

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var producto = await _productosService.GetByIdAsync(id.Value, CurrentTenantId);
            if (producto == null) return NotFound();
            return View(producto);
        }

        // ── Create GET ───────────────────────────────────────────────────────────────

        [Authorize(Roles = "Administrador,SuperAdmin")]
        public IActionResult Create() => View(new Producto());

        // ── Create POST ──────────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,SuperAdmin")]
        public async Task<IActionResult> Create(
            [Bind("Id,Nombre,Descripcion,PrecioVenta,Stock,RutaImagen")] Producto producto,
            IFormFile? productoArchivo)   // ← name="productoArchivo" en la vista
        {
            Console.WriteLine($"---> [Productos/Create] Archivo: " +
                $"{(productoArchivo == null ? "NO LLEGÓ" : $"{productoArchivo.FileName} ({productoArchivo.Length} bytes)")}");

            producto.TenantId = CurrentTenantId;
            ModelState.Remove(nameof(producto.Tenant));
            ModelState.Remove(nameof(producto.RutaImagen));

            if (!ModelState.IsValid)
            {
                foreach (var kv in ModelState)
                    foreach (var err in kv.Value.Errors)
                        Console.WriteLine($"     [ModelState] '{kv.Key}': {err.ErrorMessage}");
                return View(producto);
            }

            // Subir imagen a Cloudinary con carpeta multi-tenant
            if (productoArchivo != null && productoArchivo.Length > 0)
            {
                var url = await _storageService.SubirImagenAsync(
                    productoArchivo, $"productos/tenant_{CurrentTenantId}");

                if (!string.IsNullOrEmpty(url))
                    producto.RutaImagen = url;
            }

            await _productosService.CreateAsync(producto, CurrentTenantId, CurrentUsuarioId, CurrentUserIp);
            return RedirectToAction(nameof(Index));
        }

        // ── Edit GET ─────────────────────────────────────────────────────────────────

        [Authorize(Roles = "Administrador,SuperAdmin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var producto = await _productosService.GetByIdAsync(id.Value, CurrentTenantId);
            if (producto == null) return NotFound();
            return View(producto);
        }

        // ── Edit POST ────────────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,SuperAdmin")]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Id,Nombre,Descripcion,PrecioVenta,Stock,RutaImagen")] Producto producto,
            IFormFile? productoArchivo)   // ← name="productoArchivo" en la vista
        {
            if (id != producto.Id) return NotFound();

            producto.TenantId = CurrentTenantId;
            ModelState.Remove(nameof(producto.Tenant));
            ModelState.Remove(nameof(producto.RutaImagen));

            if (!ModelState.IsValid) return View(producto);

            try
            {
                // Si se seleccionó una imagen nueva, reemplazarla en Cloudinary
                if (productoArchivo != null && productoArchivo.Length > 0)
                {
                    var url = await _storageService.SubirImagenAsync(
                        productoArchivo, $"productos/tenant_{CurrentTenantId}");
                    if (!string.IsNullOrEmpty(url))
                        producto.RutaImagen = url;
                }
                // Si no hay imagen nueva, la vista envía la URL actual
                // vía <input type="hidden" asp-for="RutaImagen"> y se preserva.

                await _productosService.UpdateAsync(producto, CurrentTenantId, CurrentUsuarioId, CurrentUserIp);
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_productosService.Exists(producto.Id, CurrentTenantId)) return NotFound();
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        // ── Delete ───────────────────────────────────────────────────────────────────

        [Authorize(Roles = "Administrador,SuperAdmin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var producto = await _productosService.GetByIdAsync(id.Value, CurrentTenantId);
            if (producto == null) return NotFound();
            return View(producto);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,SuperAdmin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _productosService.DeleteAsync(id, CurrentTenantId, CurrentUsuarioId, CurrentUserIp);
            return RedirectToAction(nameof(Index));
        }
    }
}