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
    [Authorize]
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
        public async Task<IActionResult> Create()
        {
            var citas = await _facturaService.ObtenerCitasDisponiblesAsync();
            ViewBag.CitaId = new SelectList(citas, "Id", "Id");
            ViewBag.TenantId = new SelectList(await _facturaService.ObtenerTenantsAsync(), "Id", "NombreNegocio");

            var ahora = DateTime.Now;
            var fechaLimpia = new DateTime(ahora.Year, ahora.Month, ahora.Day, ahora.Hour, ahora.Minute, 0, 0);

            return View(new Factura { FechaPago = fechaLimpia });
        }

        // POST: Facturas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,TenantId,CitaId,Total,MetodoPago,FechaPago")] Factura factura)
        {
            ModelState.Remove(nameof(factura.Tenant));
            ModelState.Remove(nameof(factura.Cita));

            if (ModelState.IsValid)
            {
                await _facturaService.GuardarAsync(factura, CurrentUsuarioId, CurrentUserIp);
                return RedirectToAction(nameof(Index));
            }

            ViewBag.CitaId = new SelectList(await _facturaService.ObtenerCitasDisponiblesAsync(), "Id", "Id", factura.CitaId);
            ViewBag.TenantId = new SelectList(await _facturaService.ObtenerTenantsAsync(), "Id", "NombreNegocio", factura.TenantId);
            return View(factura);
        }

        // GET: Facturas/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var factura = await _facturaService.ObtenerPorIdAsync(id.Value);
            if (factura == null) return NotFound();

            ViewBag.CitaId = new SelectList(await _facturaService.ObtenerCitasDisponiblesAsync(), "Id", "Id", factura.CitaId);
            ViewBag.TenantId = new SelectList(await _facturaService.ObtenerTenantsAsync(), "Id", "NombreNegocio", factura.TenantId);
            return View(factura);
        }

        // POST: Facturas/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,TenantId,CitaId,Total,MetodoPago,FechaPago")] Factura factura)
        {
            if (id != factura.Id) return NotFound();

            ModelState.Remove(nameof(factura.Tenant));
            ModelState.Remove(nameof(factura.Cita));

            if (ModelState.IsValid)
            {
                try
                {
                    await _facturaService.GuardarAsync(factura, CurrentUsuarioId, CurrentUserIp);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_facturaService.Existe(factura.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.CitaId = new SelectList(await _facturaService.ObtenerCitasDisponiblesAsync(), "Id", "Id", factura.CitaId);
            ViewBag.TenantId = new SelectList(await _facturaService.ObtenerTenantsAsync(), "Id", "NombreNegocio", factura.TenantId);
            return View(factura);
        }

        // GET: Facturas/Delete/5
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
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _facturaService.EliminarAsync(id, CurrentUsuarioId, CurrentUserIp);
            return RedirectToAction(nameof(Index));
        }
    }
}