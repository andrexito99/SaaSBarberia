using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;
using SaaSBarberia.Services;

namespace SaaSBarberia.Controllers;

[Authorize(Roles = "Administrador")]
public class WhatsAppDashboardController : Controller
{
    private readonly BarberSaaSDbContext _context;
    private readonly WhatsAppConfiguracionService _configuracion;
    public WhatsAppDashboardController(BarberSaaSDbContext context, WhatsAppConfiguracionService configuracion) => (_context, _configuracion) = (context, configuracion);

    public async Task<IActionResult> Index()
    {
        if (!int.TryParse(User.FindFirst("TenantId")?.Value, out var tenantId) || tenantId <= 0 || !await _configuracion.ModuloHabilitadoAsync(tenantId)) return Forbid();
        var configuracion = await _context.WhatsAppConfiguraciones.AsNoTracking().FirstOrDefaultAsync(c => c.TenantId == tenantId);
        var inicioHoy = DateTime.Today;
        var mensajes = _context.WhatsAppMensajesHistorial.Where(m => m.TenantId == tenantId);
        var hoy = mensajes.Where(m => m.Fecha >= inicioHoy && m.Estado == "Enviado");
        var modelo = new WhatsAppDashboardViewModel
        {
            EstadoConexion = configuracion?.EstadoConexion ?? "No conectado",
            NumeroWhatsApp = configuracion?.NumeroWhatsApp,
            MensajesEnviadosHoy = await hoy.CountAsync(),
            RecordatoriosEnviados = await hoy.CountAsync(m => m.Tipo == "Recordatorio24Horas" || m.Tipo == "Recordatorio2Horas"),
            ConfirmacionesEnviadas = await hoy.CountAsync(m => m.Tipo == "Confirmacion"),
            CancelacionesEnviadas = await hoy.CountAsync(m => m.Tipo == "Cancelacion"),
            ReprogramacionesEnviadas = await hoy.CountAsync(m => m.Tipo == "Reprogramacion"),
            UltimosMensajes = await mensajes.Include(m => m.Cliente).OrderByDescending(m => m.Fecha).Take(10).ToListAsync(),
            ErroresRecientes = await mensajes.Where(m => m.Estado == "Error").OrderByDescending(m => m.Fecha).Take(10).ToListAsync()
        };
        return View(modelo);
    }
}
