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
            Console.WriteLine($"---> 🚨 1. ENTRO AL CREATE POST.");
            Console.WriteLine($"---> 📦 ARCHIVO RECIBIDO: {(productoArchivo == null ? "NULO / NO LLEGÓ" : productoArchivo.FileName + " (" + productoArchivo.Length + " bytes)")}");

            // Asignamos TenantId y limpiamos navegaciones para evitar falsos negativos en el ModelState
            producto.TenantId = CurrentTenantId;
            ModelState.Remove(nameof(producto.Tenant));
            ModelState.Remove(nameof(producto.RutaImagen)); // 👈 Removemos RutaImagen para que no marque error si viene vacía inicialmente

            if (!ModelState.IsValid)
            {
                Console.WriteLine("---> ❌ ERRORES DE VALIDACIÓN EN MODELSTATE:");
                foreach (var state in ModelState)
                {
                    foreach (var error in state.Value.Errors)
                    {
                        Console.WriteLine($"      Campo '{state.Key}': {error.ErrorMessage}");
                    }
                }
            }
            else
            {
                if (productoArchivo != null && productoArchivo.Length > 0)
                {
                    Console.WriteLine("---> ⏳ 2. LLAMANDO A SUPABASE STORAGE SERVICE...");
                    string? urlImagen = await _storageService.SubirImagenAsync(productoArchivo, $"productos/tenant_{CurrentTenantId}");

                    if (!string.IsNullOrEmpty(urlImagen))
                    {
                        producto.RutaImagen = urlImagen;
                        Console.WriteLine($"---> ✅ 3. RUTA GUARDADA EN MODELO: {producto.RutaImagen}");
                    }
                    else
                    {
                        Console.WriteLine("---> ❌ 3. SUPABASE RETORNÓ NULL AL SUBIR LA IMAGEN.");
                    }
                }
                else
                {
                    Console.WriteLine("---> ⚠️ NO SE SELECCIONÓ NINGÚN ARCHIVO DE IMAGEN.");
                }

                await _productosService.CreateAsync(producto, CurrentTenantId, CurrentUsuarioId, CurrentUserIp);
                Console.WriteLine("---> 🚀 4. PRODUCTO GUARDADO EN BD EXITOSAMENTE.");
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

            producto.TenantId = CurrentTenantId;
            ModelState.Remove(nameof(producto.Tenant));
            ModelState.Remove(nameof(producto.RutaImagen));

            if (ModelState.IsValid)
            {
                try
                {
                    if (productoArchivo != null && productoArchivo.Length > 0)
                    {
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