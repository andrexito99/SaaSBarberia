using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSBarberia.Models;
using SaaSBarberia.Services;

namespace SaaSBarberia.Controllers;

[Authorize(Roles = "Administrador")]
public class WhatsAppConfiguracionController : Controller
{
    private readonly WhatsAppConfiguracionService _configuraciones;
    private readonly IWhatsAppService _whatsApp;
    public WhatsAppConfiguracionController(WhatsAppConfiguracionService configuraciones, IWhatsAppService whatsApp) => (_configuraciones, _whatsApp) = (configuraciones, whatsApp);
    private bool TryTenant(out int tenantId) => int.TryParse(User.FindFirst("TenantId")?.Value, out tenantId) && tenantId > 0;

    public async Task<IActionResult> Index()
    {
        if (!TryTenant(out var tenantId) || !await _configuraciones.ModuloHabilitadoAsync(tenantId)) return Forbid();
        return View(await _configuraciones.ObtenerParaEdicionAsync(tenantId));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(WhatsAppConfiguracionViewModel modelo)
    {
        if (!TryTenant(out var tenantId) || !await _configuraciones.ModuloHabilitadoAsync(tenantId)) return Forbid();
        if (!ModelState.IsValid) return View(modelo);
        try { await _configuraciones.GuardarAsync(tenantId, modelo); TempData["Mensaje"] = "Configuración de WhatsApp guardada."; return RedirectToAction(nameof(Index)); }
        catch (InvalidOperationException ex) { ModelState.AddModelError(nameof(modelo.NuevoAccessToken), ex.Message); return View(modelo); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Conectar(WhatsAppConfiguracionViewModel modelo)
    {
        if (!TryTenant(out var tenantId) || !await _configuraciones.ModuloHabilitadoAsync(tenantId)) return Forbid();
        modelo.ConexionActiva = true;
        if (!ModelState.IsValid) return View("Index", modelo);
        try { await _configuraciones.GuardarAsync(tenantId, modelo); TempData["Mensaje"] = "WhatsApp quedó conectado."; return RedirectToAction(nameof(Index)); }
        catch (InvalidOperationException ex) { ModelState.AddModelError(nameof(modelo.NuevoAccessToken), ex.Message); return View("Index", modelo); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Desconectar()
    {
        if (!TryTenant(out var tenantId) || !await _configuraciones.ModuloHabilitadoAsync(tenantId)) return Forbid();
        await _configuraciones.DesconectarAsync(tenantId);
        TempData["Mensaje"] = "WhatsApp fue desconectado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EnviarPrueba(string telefonoPrueba)
    {
        if (!TryTenant(out var tenantId) || !await _configuraciones.ModuloHabilitadoAsync(tenantId)) return Forbid();
        if (string.IsNullOrWhiteSpace(telefonoPrueba)) { TempData["Error"] = "Ingresa un número para el mensaje de prueba."; return RedirectToAction(nameof(Index)); }
        await _whatsApp.EnviarMensajePruebaAsync(tenantId, telefonoPrueba);
        TempData["Mensaje"] = "El intento de mensaje de prueba fue registrado en el historial.";
        return RedirectToAction(nameof(Index));
    }
}
