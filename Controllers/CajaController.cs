using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSBarberia.Services;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SaaSBarberia.Controllers;

[Authorize(Roles = "AdminBarberia")] // 🔒 Solo administradores tocan la caja
public class CajaController : Controller
{
    private readonly CajaService _cajaService;

    public CajaController(CajaService cajaService)
    {
        _cajaService = cajaService;
    }

    // Helpers dinámicos para capturar rastro del administrador
    private int CurrentUsuarioId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId) ? userId : 0;
    private string CurrentUserIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

    public async Task<IActionResult> Index()
    {
        var cajaActiva = await _cajaService.ObtenerCajaActivaAsync();
        return View(cajaActiva);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Abrir(decimal montoInicial)
    {
        if (CurrentUsuarioId == 0) return RedirectToAction("Login", "Account");

        // Se puede sacar de los claims si lo guardaste en el login, sino por defecto se asocia al tenant del admin
        var tenantId = int.Parse(User.FindFirst("TenantId")?.Value ?? "1");

        await _cajaService.AbrirCajaAsync(montoInicial, CurrentUsuarioId, tenantId, CurrentUserIp);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegistrarMovimiento(decimal monto, string tipo, string descripcion)
    {
        if (CurrentUsuarioId == 0) return RedirectToAction("Login", "Account");

        var cajaActiva = await _cajaService.ObtenerCajaActivaAsync();
        if (cajaActiva != null)
        {
            await _cajaService.RegistrarMovimientoManualAsync(cajaActiva.Id, monto, tipo, descripcion, CurrentUsuarioId, CurrentUserIp);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cerrar(decimal montoReal)
    {
        if (CurrentUsuarioId == 0) return RedirectToAction("Login", "Account");

        var cajaActiva = await _cajaService.ObtenerCajaActivaAsync();
        if (cajaActiva != null)
        {
            await _cajaService.CerrarCajaAsync(cajaActiva.Id, montoReal, CurrentUsuarioId, CurrentUserIp);
        }
        return RedirectToAction(nameof(Index));
    }
}