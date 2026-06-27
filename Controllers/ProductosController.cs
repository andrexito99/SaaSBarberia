using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Filters;
using SaaSBarberia.Models;
using SaaSBarberia.Services;

namespace SaaSBarberia.Controllers
{
    [Authorize]
    [TenantAuthorize]
    public class ProductosController : Controller
    {
        private readonly IProductosService _productosService;

        public ProductosController(IProductosService productosService)
        {
            _productosService = productosService;
        }

        // 🔑 PROPIEDAD DINÁMICA: Extrae de forma segura el TenantId del Claim de la Cookie
        private int CurrentTenantId
        {
            get
            {
                var tenantClaim = User.FindFirst("TenantId")?.Value;
                if (string.IsNullOrEmpty(tenantClaim) || !int.TryParse(tenantClaim, out int tenantId))
                {
                    throw new UnauthorizedAccessException("No se pudo identificar el Tenant del usuario autenticado.");
                }
                return tenantId;
            }
        }

        // 🔍 MÉTODOS DE AYUDA INTERNOS PARA EXTRAER USUARIO E IP (Para la Auditoría)
        private int CurrentUsuarioId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId) ? userId : 0;
        private string CurrentUserIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        // =======================================================================
        // 🔓 VISTAS COMPARTIDAS (Entran Clientes, AdminBarberia y SuperAdmin)
        // =======================================================================

        // GET: Productos
        public async Task<IActionResult> Index()
        {
            var productos = await _productosService.GetAllAsync(CurrentTenantId);
            return View(productos);
        }

        // GET: Productos/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var producto = await _productosService.GetByIdAsync(id.Value, CurrentTenantId);
            if (producto == null) return NotFound();

            return View(producto);
        }

        // =======================================================================
        // 🚨 MÉTODOS ADMINISTRATIVOS (El Cliente tiene el acceso totalmente denegado)
        // =======================================================================

        // GET: Productos/Create
        [Authorize(Roles = "AdminBarberia,SuperAdmin")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Productos/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "AdminBarberia,SuperAdmin")]
        public async Task<IActionResult> Create([Bind("Id,Nombre,Descripcion,PrecioVenta,Stock")] Producto producto)
        {
            if (ModelState.IsValid)
            {
                await _productosService.CreateAsync(producto, CurrentTenantId, CurrentUsuarioId, CurrentUserIp);
                return RedirectToAction(nameof(Index));
            }
            return View(producto);
        }

        // GET: Productos/Edit/5
        [Authorize(Roles = "AdminBarberia,SuperAdmin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var producto = await _productosService.GetByIdAsync(id.Value, CurrentTenantId);
            if (producto == null) return NotFound();

            return View(producto);
        }

        // POST: Productos/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "AdminBarberia,SuperAdmin")]
        public async Task<IActionResult> Edit(int? id, [Bind("Id,Nombre,Descripcion,PrecioVenta,Stock")] Producto producto)
        {
            if (id != producto.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
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

        // GET: Productos/Delete/5
        [Authorize(Roles = "AdminBarberia,SuperAdmin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var producto = await _productosService.GetByIdAsync(id.Value, CurrentTenantId);
            if (producto == null) return NotFound();

            return View(producto);
        }

        // POST: Productos/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "AdminBarberia,SuperAdmin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _productosService.DeleteAsync(id, CurrentTenantId, CurrentUsuarioId, CurrentUserIp);
            return RedirectToAction(nameof(Index));
        }
    }
}