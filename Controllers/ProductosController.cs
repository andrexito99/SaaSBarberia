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
        private readonly SupabaseStorageService _storageService;

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
            // 👈 ASIGNAMOS EL TENANTID OBLIGATORIO AL MODELO
            producto.TenantId = CurrentTenantId;
            ModelState.Remove(nameof(producto.Tenant));

            if (ModelState.IsValid)
            {
                if (productoArchivo != null && productoArchivo.Length > 0)
                {
                    // ☁️ Subir imagen a Supabase Storage con aislamiento Multi-Tenant
                    string? urlImagen = await _storageService.SubirImagenAsync(productoArchivo, $"productos/tenant_{CurrentTenantId}");
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

            // 👈 MANTENEMOS EL TENANTID CORRECTO EN LA EDICIÓN
            producto.TenantId = CurrentTenantId;
            ModelState.Remove(nameof(producto.Tenant));

            if (ModelState.IsValid)
            {
                try
                {
                    if (productoArchivo != null && productoArchivo.Length > 0)
                    {
                        // ☁️ Subir la nueva imagen a Supabase Storage con aislamiento Multi-Tenant
                        string? urlImagen = await _storageService.SubirImagenAsync(productoArchivo, $"productos/tenant_{CurrentTenantId}");
                        if (!string.IsNullOrEmpty(urlImagen))
                        {
                            producto.RutaImagen = urlImagen;
                        }
                    }

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
            await _productosService.DeleteAsync(id, CurrentTenantId, CurrentUsuarioId, CurrentUserIp);
            return RedirectToAction(nameof(Index));
        }
    }
}