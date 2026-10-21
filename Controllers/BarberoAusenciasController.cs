using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SaaSBarberia.Models;
using SaaSBarberia.Services;

namespace SaaSBarberia.Controllers;

[Authorize(Roles = "Administrador,SuperAdmin")]
public class BarberoAusenciasController : Controller
{
    private readonly BarberoAusenciaService _ausencias;
    public BarberoAusenciasController(BarberoAusenciaService ausencias) => _ausencias = ausencias;

    private bool TryObtenerContexto(out int tenantId, out int usuarioId)
    {
        tenantId = 0; usuarioId = 0;
        return int.TryParse(User.FindFirst("TenantId")?.Value, out tenantId) && tenantId > 0 &&
               int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out usuarioId) && usuarioId > 0;
    }
    private string IpActual => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

    public async Task<IActionResult> Index()
    {
        if (!TryObtenerContexto(out var tenantId, out _)) return Forbid();
        return View(await _ausencias.ObtenerAsync(tenantId));
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null || !TryObtenerContexto(out var tenantId, out _)) return NotFound();
        var ausencia = await _ausencias.ObtenerPorIdAsync(id.Value, tenantId);
        return ausencia == null ? NotFound() : View(ausencia);
    }

    public async Task<IActionResult> Create()
    {
        if (!TryObtenerContexto(out var tenantId, out _)) return Forbid();
        await CargarBarberosAsync(tenantId);
        return View(new BarberoAusencia { FechaInicio = DateTime.Now.AddHours(1), FechaFin = DateTime.Now.AddHours(2) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("BarberoId,FechaInicio,FechaFin,Tipo,Motivo,Observaciones")] BarberoAusencia ausencia)
    {
        if (!TryObtenerContexto(out var tenantId, out var usuarioId)) return Forbid();

        // 🔥 CORRECCIÓN CLAVE: Removemos la validación automática para los campos que el formulario no envía
        ModelState.Remove("CreadoPorUsuarioId");
        ModelState.Remove("Barbero");

        if (ModelState.IsValid)
        {
            try
            {
                await _ausencias.CrearAsync(ausencia, tenantId, usuarioId, IpActual);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }

        await CargarBarberosAsync(tenantId, ausencia.BarberoId);
        return View(ausencia);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null || !TryObtenerContexto(out var tenantId, out _)) return NotFound();
        var ausencia = await _ausencias.ObtenerPorIdAsync(id.Value, tenantId);
        if (ausencia == null) return NotFound();
        await CargarBarberosAsync(tenantId, ausencia.BarberoId);
        return View(ausencia);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,BarberoId,FechaInicio,FechaFin,Tipo,Motivo,Observaciones,Activo")] BarberoAusencia ausencia)
    {
        if (id != ausencia.Id) return NotFound();
        if (!TryObtenerContexto(out var tenantId, out var usuarioId)) return Forbid();

        // 🔥 CORRECCIÓN CLAVE: Al editar también evitamos que llore por el creador y la navegación
        ModelState.Remove("CreadoPorUsuarioId");
        ModelState.Remove("Barbero");

        if (ModelState.IsValid)
        {
            try
            {
                await _ausencias.ActualizarAsync(ausencia, tenantId, usuarioId, IpActual);
                return RedirectToAction(nameof(Index));
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }
        await CargarBarberosAsync(tenantId, ausencia.BarberoId);
        return View(ausencia);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null || !TryObtenerContexto(out var tenantId, out _)) return NotFound();
        var ausencia = await _ausencias.ObtenerPorIdAsync(id.Value, tenantId);
        return ausencia == null ? NotFound() : View(ausencia);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (!TryObtenerContexto(out var tenantId, out var usuarioId)) return Forbid();
        await _ausencias.EliminarAsync(id, tenantId, usuarioId, IpActual);
        return RedirectToAction(nameof(Index));
    }

    private async Task CargarBarberosAsync(int tenantId, int? seleccionado = null)
    {
        var barberos = await _ausencias.ObtenerBarberosAsync(tenantId);
        ViewBag.BarberoId = new SelectList(barberos.Select(b => new { b.Id, Nombre = b.Usuario.Nombre }), "Id", "Nombre", seleccionado);
    }
}