using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSBarberia.Services;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SaaSBarberia.Controllers;

[Authorize(Roles = "AdminBarberia")] // 🔒 Blindado solo para Administradores
public class CajaController : Controller
{
    private readonly CajaService _cajaService;

    public CajaController(CajaService cajaService)
    {
        _cajaService = cajaService;
    }

    public async Task<IActionResult> Index()
    {
        var cajaActiva = await _cajaService.ObtenerCajaActivaAsync();
        return View(cajaActiva);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Abrir(decimal montoInicial)
    {
        var usuarioId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        var tenantId = int.Parse(User.FindFirst("TenantId")?.Value ?? "0"); //

        await _cajaService.AbrirCajaAsync(montoInicial, usuarioId, tenantId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarMovimiento(decimal monto, string tipo, string descripcion)
    {
        var cajaActiva = await _cajaService.ObtenerCajaActivaAsync();
        if (cajaActiva != null)
        {
            await _cajaService.RegistrarMovimientoManualAsync(cajaActiva.Id, monto, tipo, descripcion);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cerrar(decimal montoReal)
    {
        var cajaActiva = await _cajaService.ObtenerCajaActivaAsync();
        if (cajaActiva != null)
        {
            await _cajaService.CerrarCajaAsync(cajaActiva.Id, montoReal);
        }
        return RedirectToAction(nameof(Index));
    }
}