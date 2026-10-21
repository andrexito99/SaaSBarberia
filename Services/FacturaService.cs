using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;

namespace SaaSBarberia.Services
{
    public class FacturaService
    {
        private readonly BarberSaaSDbContext _context;
        private readonly IAuditoriaService _auditoriaService; // 🚨 El vigilante inyectado

        public FacturaService(BarberSaaSDbContext context, IAuditoriaService auditoriaService)
        {
            _context = context;
            _auditoriaService = auditoriaService;
        }

        private IQueryable<Factura> FacturasDelTenantActual()
        {
            var facturas = _context.Facturas.AsQueryable();
            if (_context.EsSuperAdmin) return facturas;

            var tenantId = _context.TenantIdActual;
            return tenantId.HasValue && tenantId.Value > 0
                ? facturas.Where(f => f.TenantId == tenantId.Value)
                : facturas.Where(_ => false);
        }

        public async Task<List<Factura>> ObtenerTodasConRelacionesAsync()
        {
            return await FacturasDelTenantActual()
                .Include(f => f.Cita)
                .Include(f => f.Tenant)
                .ToListAsync();
        }

        public async Task<Factura?> ObtenerPorIdAsync(int id)
        {
            return await FacturasDelTenantActual()
                .Include(f => f.Cita)
                .Include(f => f.Tenant)
                .FirstOrDefaultAsync(f => f.Id == id);
        }

        private async Task<bool> CitaPerteneceAlTenantAsync(int citaId, int tenantId)
        {
            return citaId > 0 && tenantId > 0 &&
                await _context.Citas.AnyAsync(c => c.Id == citaId && c.TenantId == tenantId);
        }

        private async Task<bool> TenantExisteAsync(int tenantId)
        {
            return tenantId > 0 && await _context.Tenants.AnyAsync(t => t.Id == tenantId);
        }

        // 💾 GUARDAR / ACTUALIZAR FACTURA CON AUDITORÍA DE DINERO
        public async Task<bool> GuardarAsync(Factura factura, int usuarioId, string ip)
        {
            if (factura.Id == 0)
            {
                if (!_context.EsSuperAdmin)
                {
                    var tenantIdActual = _context.TenantIdActual;
                    if (!tenantIdActual.HasValue || tenantIdActual.Value <= 0) return false;
                    factura.TenantId = tenantIdActual.Value;
                }

                if (!await TenantExisteAsync(factura.TenantId) || !await CitaPerteneceAlTenantAsync(factura.CitaId, factura.TenantId))
                {
                    return false;
                }

                _context.Add(factura);
                await _context.SaveChangesAsync(); // Guardamos para generar Id de factura

                // 🔥 AUDITORÍA: Reporte de nueva venta ingresada
                await _auditoriaService.RegistrarAsync(
                    factura.TenantId,
                    usuarioId,
                    "Factura",
                    factura.Id.ToString(),
                    "Crear",
                    $"Se generó la factura ID {factura.Id} asociada a la Cita ID {factura.CitaId}. Total cobrado: ${factura.Total.ToString("N2")} usando método de pago: {factura.MetodoPago}.",
                    ip
                );

                return true;
            }

            // Se obtiene la entidad dentro del Tenant autorizado y se copian solo los campos editables.
            // Nunca se adjunta ni se actualiza una entidad proveniente directamente del request.
            var original = await FacturasDelTenantActual().FirstOrDefaultAsync(f => f.Id == factura.Id);
            if (original == null || !await CitaPerteneceAlTenantAsync(factura.CitaId, original.TenantId)) return false;

            var totalAnterior = original.Total;
            var metodoPagoAnterior = original.MetodoPago;
            var fechaPagoAnterior = original.FechaPago;

            original.CitaId = factura.CitaId;
            original.Total = factura.Total;
            original.MetodoPago = factura.MetodoPago;
            original.FechaPago = factura.FechaPago;
            await _context.SaveChangesAsync();

            string cambios = $"Se modificó la factura ID {original.Id}. ";
            if (totalAnterior != original.Total)
                cambios += $"Monto Alterado: ${totalAnterior.ToString("N2")} -> ${original.Total.ToString("N2")}. ";
            if (metodoPagoAnterior != original.MetodoPago)
                cambios += $"Método: '{metodoPagoAnterior}' -> '{original.MetodoPago}'. ";
            if (fechaPagoAnterior != original.FechaPago)
                cambios += $"Fecha: {fechaPagoAnterior:dd/MM/yyyy} -> {original.FechaPago:dd/MM/yyyy}. ";

            // 🔥 AUDITORÍA: Alerta de edición de dinero
            await _auditoriaService.RegistrarAsync(original.TenantId, usuarioId, "Factura", original.Id.ToString(), "Editar", cambios, ip);
            return true;
        }

        // 🗑️ ELIMINAR FACTURA CON RASTRO IMPECABLE
        public async Task<bool> EliminarAsync(int id, int usuarioId, string ip)
        {
            var factura = await FacturasDelTenantActual().FirstOrDefaultAsync(f => f.Id == id);
            if (factura == null) return false;

            int tenantId = factura.TenantId;
            string detallesEliminados = $"Factura ID {factura.Id} (Cita ID: {factura.CitaId}). Total que desapareció del sistema: ${factura.Total.ToString("N2")}, pagada con {factura.MetodoPago} el {factura.FechaPago:dd/MM/yyyy}.";

            _context.Facturas.Remove(factura);
            await _context.SaveChangesAsync();

            // 🔥 AUDITORÍA: El rastro imborrable de la plata borrada
            await _auditoriaService.RegistrarAsync(
                tenantId,
                usuarioId,
                "Factura",
                id.ToString(),
                "Eliminar",
                $"¡ALERTA! Se ELIMINÓ por completo la {detallesEliminados}",
                ip
            );

            return true;
        }

        public bool Existe(int id)
        {
            return FacturasDelTenantActual().Any(e => e.Id == id);
        }

        public async Task<List<Cita>> ObtenerCitasDisponiblesAsync()
        {
            return await _context.Citas.ToListAsync();
        }

        public async Task<List<Tenant>> ObtenerTenantsAsync()
        {
            if (_context.EsSuperAdmin) return await _context.Tenants.ToListAsync();

            var tenantId = _context.TenantIdActual;
            return tenantId.HasValue && tenantId.Value > 0
                ? await _context.Tenants.Where(t => t.Id == tenantId.Value).ToListAsync()
                : new List<Tenant>();
        }
    }
}
