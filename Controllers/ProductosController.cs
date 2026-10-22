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
        private readonly SupabaseStorageService _storageService; // 🚀 Reemplazamos IWebHostEnvironment por Supabase Storage

        public ProductosController(IProductosService productosService, SupabaseStorageService storageService)
        {
            _productosService = productosService;
            _storageService = storageService;
        }

        private int CurrentTenantId
        {
            get
            {
                var tenantClaim = User.FindFirst("TenantId")?.Value;
                if (string.IsNullOrEmpty(tenantClaim) || !int.TryParse(tenantClaim, out int tenantId))
                {
                    throw new UnauthorizedAccessException("No se pudo identificar el Tenant.");
                }
                return tenantId;
            }
        }

        private int CurrentUsuarioId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId) ? userId : 0;
        private string CurrentUserIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        public async Task<IActionResult> Index()
        {
            var productos = await _productosService.GetAllAsync(CurrentTenantId);
            return View(productos);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var producto = await _productosService.GetByIdAsync(id.Value, CurrentTenantId);
            if (producto == null) return NotFound();
            return View(producto);
        }

        [Authorize(Roles = "Administrador,SuperAdmin")]
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,SuperAdmin")]
        public async Task<IActionResult> Create([Bind("Id,Nombre,Descripcion,PrecioVenta,Stock,RutaImagen")] Producto producto, IFormFile? productoArchivo)
        {
            if (ModelState.IsValid)
            {
                if (productoArchivo != null && productoArchivo.Length > 0)
                {
                    // ☁️ Subir imagen a la carpeta "productos" en Supabase Storage
                    string? urlImagen = await _storageService.SubirImagenAsync(productoArchivo, "productos");
                    if (!string.IsNullOrEmpty(urlImagen))
                    {
                        producto.RutaImagen = urlImagen;
                    }
                }

                await _productosService.CreateAsync(producto, CurrentTenantId, CurrentUsuarioId, CurrentUserIp);
                return RedirectToAction(nameof(Index));
            }
            return View(producto);
        }

        [Authorize(Roles = "Administrador,SuperAdmin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var producto = await _productosService.GetByIdAsync(id.Value, CurrentTenantId);
            if (producto == null) return NotFound();
            return View(producto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,SuperAdmin")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nombre,Descripcion,PrecioVenta,Stock,RutaImagen")] Producto producto, IFormFile? productoArchivo)
        {
            if (id != producto.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    if (productoArchivo != null && productoArchivo.Length > 0)
                    {
                        // ☁️ Subir la nueva imagen a Supabase Storage
                        string? urlImagen = await _storageService.SubirImagenAsync(productoArchivo, "productos");
                        if (!string.IsNullOrEmpty(urlImagen))
                        {
                            producto.RutaImagen = urlImagen;
                        }
                    }
                    // Si 'productoArchivo' es null, 'producto.RutaImagen' conserva el valor viejo gracias al campo hidden de la vista.

                    await _productosService.UpdateAsync(producto, CurrentTenantId, CurrentUsuarioId, CurrentUserIp);
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_productosService.Exists(producto.Id, CurrentTenantId)) return NotFound();
                    else throw;
                }
                catch (UnauthorizedAccessException)
                {
                    return Forbid();
                }
            }
            return View(producto);
        }

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
            // Nota: En Supabase Storage las eliminaciones físicas de archivos se pueden omitir o gestionar desde el bucket, 
            // con borrar el registro de la BD es más que suficiente para liberar la referencia.
            await _productosService.DeleteAsync(id, CurrentTenantId, CurrentUsuarioId, CurrentUserIp);
            return RedirectToAction(nameof(Index));
        }
    }
}