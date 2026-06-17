using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Models;
using SaaSBarberia.Services;
using System;
using System.Threading.Tasks;

public class FacturasController : Controller
{
    private readonly FacturaService _facturaService;

    public FacturasController(FacturaService facturaService)
    {
        _facturaService = facturaService;
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
    public async Task<IActionResult> Create()
    {
        var citas = await _facturaService.ObtenerCitasDisponiblesAsync();
        ViewBag.CitaId = new SelectList(citas, "Id", "Id");
        ViewBag.TenantId = new SelectList(await _facturaService.ObtenerTenantsAsync(), "Id", "NombreNegocio");

        // 🌟 CORRECCIÓN: Limpiamos los segundos y milisegundos del DateTime actual
        var ahora = DateTime.Now;
        var fechaLimpia = new DateTime(ahora.Year, ahora.Month, ahora.Day, ahora.Hour, ahora.Minute, 0, 0);

        return View(new Factura { FechaPago = fechaLimpia });
    }

    // POST: Facturas/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,TenantId,CitaId,Total,MetodoPago,FechaPago")] Factura factura)
    {
        // 🌟 CORRECCIÓN: Remoción estratégica de relaciones virtuales para que pase la validación básica
        ModelState.Remove(nameof(factura.Tenant));
        ModelState.Remove(nameof(factura.Cita));

        if (ModelState.IsValid)
        {
            await _facturaService.GuardarAsync(factura);
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

        // 🌟 CORRECCIÓN: También removemos los objetos virtuales en el Edit
        ModelState.Remove(nameof(factura.Tenant));
        ModelState.Remove(nameof(factura.Cita));

        if (ModelState.IsValid)
        {
            try
            {
                await _facturaService.GuardarAsync(factura);
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
        await _facturaService.EliminarAsync(id);
        return RedirectToAction(nameof(Index));
    }
}