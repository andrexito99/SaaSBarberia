using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSBarberia.Models;
using SaaSBarberia.Services;

namespace SaaSBarberia.Controllers;

[Authorize(Roles = "Administrador")]
public class WhatsAppPlantillasController : Controller
{
    private readonly WhatsAppConfiguracionService _configuraciones;
    private readonly WhatsAppPlantillaService _plantillas;
    public WhatsAppPlantillasController(WhatsAppConfiguracionService configuraciones, WhatsAppPlantillaService plantillas) => (_configuraciones, _plantillas) = (configuraciones, plantillas);
    private bool TryTenant(out int tenantId) => int.TryParse(User.FindFirst("TenantId")?.Value, out tenantId) && tenantId > 0;
    private async Task<bool> TieneAccesoAsync(int tenantId) => await _configuraciones.ModuloHabilitadoAsync(tenantId);

    public async Task<IActionResult> Index()
    {
        if (!TryTenant(out var tenantId) || !await TieneAccesoAsync(tenantId)) return Forbid();
        await _plantillas.CrearPlantillasInicialesSiFaltanAsync(tenantId);
        return View(await _plantillas.ObtenerAsync(tenantId));
    }
    public async Task<IActionResult> Edit(int? id)
    {
        if (!TryTenant(out var tenantId) || !await TieneAccesoAsync(tenantId)) return Forbid();
        if (id == null) return View(new WhatsAppPlantilla());
        var plantilla = await _plantillas.ObtenerAsync(id.Value, tenantId);
        return plantilla == null ? NotFound() : View(plantilla);
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([Bind("Id,Tipo,Contenido,Activa")] WhatsAppPlantilla plantilla)
    {
        if (!TryTenant(out var tenantId) || !await TieneAccesoAsync(tenantId)) return Forbid();
        if (!TiposPermitidos.Contains(plantilla.Tipo)) ModelState.AddModelError(nameof(plantilla.Tipo), "Tipo de plantilla no válido.");
        if (!ModelState.IsValid) return View(plantilla);
        try { await _plantillas.GuardarAsync(plantilla, tenantId); return RedirectToAction(nameof(Index)); }
        catch (InvalidOperationException ex) { ModelState.AddModelError(nameof(plantilla.Tipo), ex.Message); return View(plantilla); }
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!TryTenant(out var tenantId) || !await TieneAccesoAsync(tenantId)) return Forbid();
        await _plantillas.EliminarAsync(id, tenantId);
        return RedirectToAction(nameof(Index));
    }
    public static readonly string[] TiposPermitidos = WhatsAppPlantillaService.TiposRequeridos;
}
