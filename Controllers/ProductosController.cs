using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Filters;
using SaaSBarberia.Models;
using SaaSBarberia.Services;
using Microsoft.AspNetCore.Hosting;

namespace SaaSBarberia.Controllers
{
    [Authorize]
    [TenantAuthorize]
    public class ProductosController : Controller
    {
        private readonly IProductosService _productosService;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProductosController(IProductosService productosService, IWebHostEnvironment webHostEnvironment)
        {
            _productosService = productosService;
            _webHostEnvironment = webHostEnvironment;
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

        [Authorize(Roles = "AdminBarberia,SuperAdmin")]
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "AdminBarberia,SuperAdmin")]
        // 🔥 CORREGIDO: Cambiado 'ImagenUrl' por 'RutaImagen' en el Bind
        public async Task<IActionResult> Create([Bind("Id,Nombre,Descripcion,PrecioVenta,Stock,RutaImagen")] Producto producto, IFormFile? productoArchivo)
        {
            if (ModelState.IsValid)
            {
                if (productoArchivo != null && productoArchivo.Length > 0)
                {
                    string carpetaSubidas = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "productos");
                    if (!Directory.Exists(carpetaSubidas)) Directory.CreateDirectory(carpetaSubidas);

                    string nombreUnico = Guid.NewGuid().ToString() + Path.GetExtension(productoArchivo.FileName);
                    string rutaCompleta = Path.Combine(carpetaSubidas, nombreUnico);

                    using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                    {
                        await productoArchivo.CopyToAsync(stream);
                    }
                    producto.RutaImagen = "/uploads/productos/" + nombreUnico;
                }

                await _productosService.CreateAsync(producto, CurrentTenantId, CurrentUsuarioId, CurrentUserIp);
                return RedirectToAction(nameof(Index));
            }
            return View(producto);
        }

        [Authorize(Roles = "AdminBarberia,SuperAdmin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var producto = await _productosService.GetByIdAsync(id.Value, CurrentTenantId);
            if (producto == null) return NotFound();
            return View(producto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "AdminBarberia,SuperAdmin")]
        // 🔥 CORREGIDO: Mapeo correcto de 'RutaImagen' en el Bind para no perder el hidden
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nombre,Descripcion,PrecioVenta,Stock,RutaImagen")] Producto producto, IFormFile? productoArchivo)
        {
            if (id != producto.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    if (productoArchivo != null && productoArchivo.Length > 0)
                    {
                        // 🗑️ Borrar imagen vieja del disco duro
                        var prodExistente = await _productosService.GetByIdAsync(producto.Id, CurrentTenantId);
                        if (prodExistente != null && !string.IsNullOrEmpty(prodExistente.RutaImagen))
                        {
                            string rutaVieja = Path.Combine(_webHostEnvironment.WebRootPath, prodExistente.RutaImagen.TrimStart('/'));
                            if (System.IO.File.Exists(rutaVieja)) System.IO.File.Delete(rutaVieja);
                        }

                        // 💾 Guardar la nueva imagen
                        string carpetaSubidas = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "productos");
                        if (!Directory.Exists(carpetaSubidas)) Directory.CreateDirectory(carpetaSubidas);

                        string nombreUnico = Guid.NewGuid().ToString() + Path.GetExtension(productoArchivo.FileName);
                        string rutaCompleta = Path.Combine(carpetaSubidas, nombreUnico);

                        using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                        {
                            await productoArchivo.CopyToAsync(stream);
                        }
                        producto.RutaImagen = "/uploads/productos/" + nombreUnico;
                    }
                    // Si 'productoArchivo' es null, 'producto.RutaImagen' ya conserva el valor viejo gracias al Bind corregido y al hidden de la vista.

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

        [Authorize(Roles = "AdminBarberia,SuperAdmin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var producto = await _productosService.GetByIdAsync(id.Value, CurrentTenantId);
            if (producto == null) return NotFound();
            return View(producto);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "AdminBarberia,SuperAdmin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var producto = await _productosService.GetByIdAsync(id, CurrentTenantId);
            if (producto != null && !string.IsNullOrEmpty(producto.RutaImagen))
            {
                string rutaArchivo = Path.Combine(_webHostEnvironment.WebRootPath, producto.RutaImagen.TrimStart('/'));
                if (System.IO.File.Exists(rutaArchivo)) System.IO.File.Delete(rutaArchivo);
            }

            await _productosService.DeleteAsync(id, CurrentTenantId, CurrentUsuarioId, CurrentUserIp);
            return RedirectToAction(nameof(Index));
        }
    }
}