using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Models;
using SaaSBarberia.Services;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SaaSBarberia.Controllers
{
    [Authorize(Roles = "Administrador,SuperAdmin")]
    public class FacturasController : Controller
    {
        private readonly FacturaService _facturaService;

        public FacturasController(FacturaService facturaService)
        {
            _facturaService = facturaService;
        }

        // 🔍 PROPIEDADES DINÁMICAS DE ENTRADA DE AUDITORÍA
        private int CurrentUsuarioId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId) ? userId : 0;
        private string CurrentUserIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        private bool EsSuperAdmin => User.IsInRole("SuperAdmin");

        private bool TryAsignarTenantActual(Factura factura)
        {
            if (EsSuperAdmin) return true;

            if (!int.TryParse(User.FindFirst("TenantId")?.Value, out int tenantId) || tenantId <= 0)
            {
                return false;
            }

            // El Tenant de un usuario de barbería siempre procede del claim autenticado.
            factura.TenantId = tenantId;
            ModelState.Remove(nameof(Factura.TenantId));
            return true;
        }

        private async Task CargarCombosAsync(Factura factura)
        {
            ViewBag.CitaId = new SelectList(await _facturaService.ObtenerCitasDisponiblesAsync(), "Id", "Id", factura.CitaId);
            ViewBag.TenantId = new SelectList(await _facturaService.ObtenerTenantsAsync(), "Id", "NombreNegocio", factura.TenantId);
        }

        // GET: Facturas
        public async Task<IActionResult> Index()
        {
            return View(await _facturaService.ObtenerTodasConRelacionesAsync());
        }

        // GET: Facturas/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var factura = await _facturaService.ObtenerPorIdAsync(id.Value);
            if (factura == null) return NotFound();

            return View(factura);
        }

        // GET: Facturas/Create
        [Authorize(Roles = "Administrador,SuperAdmin")]
        public async Task<IActionResult> Create()
        {
            var factura = new Factura();
            if (!TryAsignarTenantActual(factura)) return Forbid();

            var citas = await _facturaService.ObtenerCitasDisponiblesAsync();
            ViewBag.CitaId = new SelectList(citas, "Id", "Id");
            ViewBag.TenantId = new SelectList(await _facturaService.ObtenerTenantsAsync(), "Id", "NombreNegocio");

            var ahora = DateTime.Now;
            var fechaLimpia = new DateTime(ahora.Year, ahora.Month, ahora.Day, ahora.Hour, ahora.Minute, 0, 0);

            factura.FechaPago = fechaLimpia;
            return View(factura);
        }

        // POST: Facturas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,SuperAdmin")]
        public async Task<IActionResult> Create([Bind("CitaId,Total,MetodoPago,FechaPago")] Factura factura, int? tenantId)
        {
            // Solo SuperAdmin puede elegir un Tenant al crear. Para los demás se ignora por completo el valor del request.
            if (EsSuperAdmin)
            {
                factura.TenantId = tenantId.GetValueOrDefault();
            }
            else if (!TryAsignarTenantActual(factura))
            {
                return Forbid();
            }

            ModelState.Remove(nameof(factura.Tenant));
            ModelState.Remove(nameof(factura.Cita));

            if (ModelState.IsValid)
            {
                if (await _facturaService.GuardarAsync(factura, CurrentUsuarioId, CurrentUserIp))
                {
                    return RedirectToAction(nameof(Index));
                }

                // No se expone si la cita o el Tenant solicitado pertenece a otra barbería.
                return NotFound();
            }

            await CargarCombosAsync(factura);
            return View(factura);
        }

        // GET: Facturas/Edit/5
        [Authorize(Roles = "Administrador,SuperAdmin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var factura = await _facturaService.ObtenerPorIdAsync(id.Value);
            if (factura == null) return NotFound();

            await CargarCombosAsync(factura);
            return View(factura);
        }

        // POST: Facturas/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,SuperAdmin")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,CitaId,Total,MetodoPago,FechaPago")] Factura factura)
        {
            if (id != factura.Id) return NotFound();

            ModelState.Remove(nameof(factura.Tenant));
            ModelState.Remove(nameof(factura.Cita));

            if (ModelState.IsValid)
            {
                try
                {
                    if (!await _facturaService.GuardarAsync(factura, CurrentUsuarioId, CurrentUserIp)) return NotFound();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_facturaService.Existe(factura.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            var existente = await _facturaService.ObtenerPorIdAsync(id);
            if (existente == null) return NotFound();
            factura.TenantId = existente.TenantId;
            await CargarCombosAsync(factura);
            return View(factura);
        }

        // GET: Facturas/Delete/5
        [Authorize(Roles = "Administrador,SuperAdmin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var factura = await _facturaService.ObtenerPorIdAsync(id.Value);
            if (factura == null) return NotFound();

            return View(factura);
        }

        // POST: Facturas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,SuperAdmin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!await _facturaService.EliminarAsync(id, CurrentUsuarioId, CurrentUserIp)) return NotFound();
            return RedirectToAction(nameof(Index));
        }
    }
}