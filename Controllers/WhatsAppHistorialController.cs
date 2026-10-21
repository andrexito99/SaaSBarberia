using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Services;

namespace SaaSBarberia.Controllers;

[Authorize(Roles = "Administrador")]
public class WhatsAppHistorialController : Controller
{
    private readonly BarberSaaSDbContext _context;
    private readonly WhatsAppConfiguracionService _configuraciones;
    public WhatsAppHistorialController(BarberSaaSDbContext context, WhatsAppConfiguracionService configuraciones) => (_context, _configuraciones) = (context, configuraciones);
    public async Task<IActionResult> Index(DateTime? fechaDesde, DateTime? fechaHasta, string? tipo, string? estado, int? clienteId, int? barberoId, int? citaId)
    {
        if (!int.TryParse(User.FindFirst("TenantId")?.Value, out var tenantId) || tenantId <= 0 || !await _configuraciones.ModuloHabilitadoAsync(tenantId)) return Forbid();
        var query = _context.WhatsAppMensajesHistorial.Where(h => h.TenantId == tenantId).Include(h => h.Cliente).Include(h => h.Barbero).ThenInclude(b => b!.Usuario).Include(h => h.Cita).AsQueryable();
        if (fechaDesde.HasValue) query = query.Where(h => h.Fecha >= fechaDesde.Value.Date);
        if (fechaHasta.HasValue) query = query.Where(h => h.Fecha < fechaHasta.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(tipo)) query = query.Where(h => h.Tipo == tipo);
        if (!string.IsNullOrWhiteSpace(estado)) query = query.Where(h => h.Estado == estado);
        if (clienteId.HasValue) query = query.Where(h => h.ClienteId == clienteId);
        if (barberoId.HasValue) query = query.Where(h => h.BarberoId == barberoId);
        if (citaId.HasValue) query = query.Where(h => h.CitaId == citaId);
        ViewBag.FechaDesde = fechaDesde?.ToString("yyyy-MM-dd");
        ViewBag.FechaHasta = fechaHasta?.ToString("yyyy-MM-dd");
        ViewBag.Tipo = tipo;
        ViewBag.Estado = estado;
        ViewBag.ClienteId = clienteId;
        ViewBag.BarberoId = barberoId;
        ViewBag.CitaId = citaId;
        ViewBag.Tipos = await _context.WhatsAppMensajesHistorial.Where(h => h.TenantId == tenantId).Select(h => h.Tipo).Distinct().OrderBy(t => t).ToListAsync();
        ViewBag.Clientes = await _context.Usuarios.Where(u => u.TenantId == tenantId).OrderBy(u => u.Nombre).ToListAsync();
        ViewBag.Barberos = await _context.Barberos.Where(b => b.TenantId == tenantId).Include(b => b.Usuario).OrderBy(b => b.Usuario.Nombre).ToListAsync();
        var historial = await query.OrderByDescending(h => h.Fecha).Take(500).ToListAsync();
        return View(historial);
    }
}
