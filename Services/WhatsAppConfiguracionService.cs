using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using SaaSBarberia.Data;
using SaaSBarberia.Models;

namespace SaaSBarberia.Services;

public class WhatsAppConfiguracionService
{
    private readonly BarberSaaSDbContext _context;
    private readonly IDataProtector _protector;
    private readonly TenantModuloService _modulos;

    public WhatsAppConfiguracionService(BarberSaaSDbContext context, IDataProtectionProvider protectionProvider, TenantModuloService modulos)
    {
        _context = context;
        _protector = protectionProvider.CreateProtector("SaaSBarberia.WhatsApp.AccessToken.v1");
        _modulos = modulos;
    }

    public async Task<bool> ModuloHabilitadoAsync(int tenantId) =>
        await _context.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == tenantId && t.Activo == true)
        && await _modulos.EstaHabilitadoAsync(tenantId, ModulosTenant.WhatsApp);

    public async Task<WhatsAppConfiguracionViewModel> ObtenerParaEdicionAsync(int tenantId)
    {
        var item = await _context.WhatsAppConfiguraciones.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(c => c.TenantId == tenantId);
        return item == null ? new WhatsAppConfiguracionViewModel() : new WhatsAppConfiguracionViewModel
        {
            NumeroWhatsApp = item.NumeroWhatsApp, PhoneNumberId = item.PhoneNumberId, BusinessAccountId = item.BusinessAccountId,
            ConexionActiva = item.ConexionActiva, FechaConexion = item.FechaConexion,
            EstadoConexion = item.EstadoConexion,
            EnviarConfirmaciones = item.EnviarConfirmaciones, EnviarRecordatorio24Horas = item.EnviarRecordatorio24Horas,
            EnviarRecordatorio2Horas = item.EnviarRecordatorio2Horas, EnviarCancelaciones = item.EnviarCancelaciones,
            EnviarReprogramaciones = item.EnviarReprogramaciones, PermitirMensajesPersonalizados = item.PermitirMensajesPersonalizados
        };
    }

    public async Task GuardarAsync(int tenantId, WhatsAppConfiguracionViewModel modelo)
    {
        var actual = await _context.WhatsAppConfiguraciones.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.TenantId == tenantId);
        if (actual == null)
        {
            if (string.IsNullOrWhiteSpace(modelo.NuevoAccessToken)) throw new InvalidOperationException("El token de acceso es obligatorio al conectar WhatsApp por primera vez.");
            actual = new WhatsAppConfiguracion { TenantId = tenantId };
            _context.WhatsAppConfiguraciones.Add(actual);
        }

        actual.NumeroWhatsApp = modelo.NumeroWhatsApp.Trim();
        actual.PhoneNumberId = modelo.PhoneNumberId.Trim();
        actual.BusinessAccountId = modelo.BusinessAccountId.Trim();
        actual.ConexionActiva = modelo.ConexionActiva;
        actual.EstadoConexion = modelo.ConexionActiva ? "Conectado" : "No conectado";
        actual.FechaConexion = modelo.ConexionActiva ? (actual.FechaConexion ?? DateTime.Now) : null;
        actual.EnviarConfirmaciones = modelo.EnviarConfirmaciones;
        actual.EnviarRecordatorio24Horas = modelo.EnviarRecordatorio24Horas;
        actual.EnviarRecordatorio2Horas = modelo.EnviarRecordatorio2Horas;
        actual.EnviarCancelaciones = modelo.EnviarCancelaciones;
        actual.EnviarReprogramaciones = modelo.EnviarReprogramaciones;
        actual.PermitirMensajesPersonalizados = modelo.PermitirMensajesPersonalizados;
        if (!string.IsNullOrWhiteSpace(modelo.NuevoAccessToken)) actual.AccessTokenProtegido = _protector.Protect(modelo.NuevoAccessToken.Trim());
        await _context.SaveChangesAsync();
    }

    public async Task DesconectarAsync(int tenantId)
    {
        var actual = await _context.WhatsAppConfiguraciones.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.TenantId == tenantId);
        if (actual == null) return;
        actual.ConexionActiva = false;
        actual.EstadoConexion = "No conectado";
        actual.FechaConexion = null;
        await _context.SaveChangesAsync();
    }

    public async Task<WhatsAppConfiguracion?> ObtenerActivaAsync(int tenantId)
    {
        var configuracion = await _context.WhatsAppConfiguraciones.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ConexionActiva);
        if (configuracion == null || string.IsNullOrWhiteSpace(configuracion.AccessTokenProtegido)) return null;
        try { configuracion.AccessTokenProtegido = _protector.Unprotect(configuracion.AccessTokenProtegido); return configuracion; }
        catch (CryptographicException) { return null; }
    }
}
