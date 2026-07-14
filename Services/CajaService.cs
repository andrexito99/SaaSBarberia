using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;
using System;
using System.Threading.Tasks;

namespace SaaSBarberia.Services;

public class CajaService
{
    private readonly BarberSaaSDbContext _context;
    private readonly IAuditoriaService _auditoriaService; // 🚨 El vigilante inyectado
    private readonly IEventosSistemaService _eventos;

    public CajaService(BarberSaaSDbContext context, IAuditoriaService auditoriaService, IEventosSistemaService eventos)
    {
        _context = context;
        _auditoriaService = auditoriaService;
        _eventos = eventos;
    }

    public async Task<MovimientoCaja?> ObtenerCajaActivaAsync()
    {
        return await _context.MovimientosCaja
            .Include(c => c.DetallesCaja)
            .FirstOrDefaultAsync(c => c.Estado == "Abierta");
    }

    // 🔓 APERTURA DE CAJA CON AUDITORÍA
    public async Task AbrirCajaAsync(decimal montoInicial, int usuarioId, int tenantId, string ip)
    {
        var nuevaCaja = new MovimientoCaja
        {
            TenantId = tenantId,
            UsuarioId = usuarioId,
            MontoInicial = montoInicial,
            MontoFinalCalculado = montoInicial,
            Estado = "Abierta",
            FechaApertura = DateTime.Now
        };

        _context.MovimientosCaja.Add(nuevaCaja);
        await _context.SaveChangesAsync();

        // 🔥 Registrar el rastro de apertura
        await _auditoriaService.RegistrarAsync(
            tenantId,
            usuarioId,
            "Caja",
            nuevaCaja.Id.ToString(),
            "Abrir Caja",
            $"Se realizó la apertura de caja chica ID {nuevaCaja.Id}. Monto inicial reportado: ${montoInicial.ToString("N2")}.",
            ip
        );
        await _eventos.PublicarAsync(new EventoSistema(TipoEventoSistema.CajaAbierta, tenantId, "Caja abierta", $"Se abrió la caja con monto inicial ${montoInicial:N2}.", "/Caja/Index"));
    }

    // 💸 MOVIMIENTOS MANUALES (INGRESOS / EGRESOS)
    public async Task RegistrarMovimientoManualAsync(int cajaId, decimal monto, string tipo, string descripcion, int usuarioId, string ip)
    {
        var caja = await _context.MovimientosCaja.FindAsync(cajaId);
        if (caja == null || caja.Estado == "Cerrada") return;

        var detalle = new DetalleCaja
        {
            MovimientoCajaId = cajaId,
            Monto = monto,
            Tipo = tipo,
            Descripcion = descripcion,
            Fecha = DateTime.Now
        };

        if (tipo == "Ingreso") caja.MontoFinalCalculado += monto;
        else if (tipo == "Egreso") caja.MontoFinalCalculado -= monto;

        _context.DetallesCaja.Add(detalle);
        await _context.SaveChangesAsync();

        // 🔥 Registrar si fue entrada o salida de plata
        await _auditoriaService.RegistrarAsync(
            caja.TenantId,
            usuarioId,
            "Caja",
            cajaId.ToString(),
            $"Movimiento_{tipo}",
            $"Registro manual de {tipo}. Monto: ${monto.ToString("N2")}. Descripción: {descripcion}. Nuevo saldo calculado: ${caja.MontoFinalCalculado.ToString("N2")}.",
            ip
        );
        await _eventos.PublicarAsync(new EventoSistema(TipoEventoSistema.CajaCerrada, caja.TenantId, "Caja cerrada", $"Se cerró la caja ID {cajaId}.", "/Caja/Index"));
    }

    // 🔒 CIERRE DE CAJA (CÁLCULO DE DESCUADRES)
    public async Task CerrarCajaAsync(int cajaId, decimal montoReal, int usuarioId, string ip)
    {
        var caja = await _context.MovimientosCaja.FindAsync(cajaId);
        if (caja == null) return;

        caja.MontoFinalReal = montoReal;
        caja.Estado = "Cerrada";
        caja.FechaCierre = DateTime.Now;

        decimal diferencia = montoReal - caja.MontoFinalCalculado;
        string estadoDescuadre = diferencia == 0
            ? "Caja cuadrada a la perfección."
            : $"¡Caja Descuadrada! Diferencia: ${(diferencia).ToString("N2")} (Calculado: ${caja.MontoFinalCalculado.ToString("N2")} vs Real: ${montoReal.ToString("N2")}).";

        await _context.SaveChangesAsync();

        // 🔥 Registrar el cierre de caja con la alerta del dinero real
        await _auditoriaService.RegistrarAsync(
            caja.TenantId,
            usuarioId,
            "Caja",
            cajaId.ToString(),
            "Cerrar Caja",
            $"Cierre de caja ID {cajaId}. {estadoDescuadre}",
            ip
        );
    }
}
