using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSBarberia.Services;

namespace SaaSBarberia.Controllers;

[Authorize]
public class NotificacionesController : Controller
{
    private readonly INotificacionService _notificaciones;
    public NotificacionesController(INotificacionService notificaciones) => _notificaciones = notificaciones;
    private bool TryContexto(out int tenantId, out int usuarioId)
    {
        tenantId = 0; usuarioId = 0;
        return int.TryParse(User.FindFirst("TenantId")?.Value, out tenantId) && tenantId > 0 &&
               int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out usuarioId) && usuarioId > 0;
    }

    public async Task<IActionResult> Index()
    {
        if (!TryContexto(out var tenantId, out var usuarioId)) return Forbid();
        return View(await _notificaciones.ObtenerNotificacionesUsuarioAsync(tenantId, usuarioId, 100));
    }

    [HttpGet]
    public async Task<IActionResult> Dropdown()
    {
        if (!TryContexto(out var tenantId, out var usuarioId)) return Forbid();
        return PartialView("_Dropdown", await _notificaciones.ObtenerNotificacionesUsuarioAsync(tenantId, usuarioId, 8));
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerNoLeidas()
    {
        if (!TryContexto(out var tenantId, out var usuarioId)) return Forbid();
        return Json(new { cantidad = await _notificaciones.ContarNoLeidasAsync(tenantId, usuarioId) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarLeida(int id)
    {
        if (!TryContexto(out var tenantId, out var usuarioId)) return Forbid();
        return Json(new { ok = await _notificaciones.MarcarComoLeidaAsync(id, tenantId, usuarioId) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarTodas()
    {
        if (!TryContexto(out var tenantId, out var usuarioId)) return Forbid();
        await _notificaciones.MarcarTodasComoLeidasAsync(tenantId, usuarioId); return Json(new { ok = true });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(int id)
    {
        if (!TryContexto(out var tenantId, out var usuarioId)) return Forbid();
        return Json(new { ok = await _notificaciones.EliminarAsync(id, tenantId, usuarioId) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarTodasLeidas()
    {
        if (!TryContexto(out var tenantId, out var usuarioId)) return Forbid();
        await _notificaciones.EliminarTodasLeidasAsync(tenantId, usuarioId); return Json(new { ok = true });
    }
}
