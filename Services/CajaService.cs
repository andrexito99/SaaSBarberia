using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;
using System;
using System.Threading.Tasks;

namespace SaaSBarberia.Services;

public class CajaService
{
    private readonly BarberSaaSDbContext _context;

    public CajaService(BarberSaaSDbContext context)
    {
        _context = context;
    }

    public async Task<MovimientoCaja> ObtenerCajaActivaAsync()
    {
        return await _context.MovimientosCaja
            .Include(c => c.DetallesCaja)
            .FirstOrDefaultAsync(c => c.Estado == "Abierta");
    }

    public async Task AbrirCajaAsync(decimal montoInicial, int usuarioId, int tenantId)
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
    }

    public async Task RegistrarMovimientoManualAsync(int cajaId, decimal monto, string tipo, string descripcion)
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
    }

    public async Task CerrarCajaAsync(int cajaId, decimal montoReal)
    {
        var caja = await _context.MovimientosCaja.FindAsync(cajaId);
        if (caja == null) return;

        caja.MontoFinalReal = montoReal;
        caja.Estado = "Cerrada";
        caja.FechaCierre = DateTime.Now;

        await _context.SaveChangesAsync();
    }
}