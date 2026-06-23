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

        public async Task<List<Factura>> ObtenerTodasConRelacionesAsync()
        {
            return await _context.Facturas
                .Include(f => f.Cita)
                .Include(f => f.Tenant)
                .ToListAsync();
        }

        public async Task<Factura?> ObtenerPorIdAsync(int id)
        {
            return await _context.Facturas
                .Include(f => f.Cita)
                .Include(f => f.Tenant)
                .FirstOrDefaultAsync(f => f.Id == id);
        }

        // 💾 GUARDAR / ACTUALIZAR FACTURA CON AUDITORÍA DE DINERO
        public async Task GuardarAsync(Factura factura, int usuarioId, string ip)
        {
            if (factura.Id == 0)
            {
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
            }
            else
            {
                // Obtenemos la factura original sin traqueo para ver si alteraron valores monetarios
                var original = await _context.Facturas
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.Id == factura.Id);

                _context.Update(factura);
                await _context.SaveChangesAsync();

                if (original != null)
                {
                    string cambios = $"Se modificó la factura ID {factura.Id}. ";
                    if (original.Total != factura.Total)
                        cambios += $"Monto Alterado: ${original.Total.ToString("N2")} -> ${factura.Total.ToString("N2")}. ";
                    if (original.MetodoPago != factura.MetodoPago)
                        cambios += $"Método: '{original.MetodoPago}' -> '{factura.MetodoPago}'. ";
                    if (original.FechaPago != factura.FechaPago)
                        cambios += $"Fecha: {original.FechaPago:dd/MM/yyyy} -> {factura.FechaPago:dd/MM/yyyy}. ";

                    // 🔥 AUDITORÍA: Alerta de edición de dinero
                    await _auditoriaService.RegistrarAsync(factura.TenantId, usuarioId, "Factura", factura.Id.ToString(), "Editar", cambios, ip);
                }
            }
        }

        // 🗑️ ELIMINAR FACTURA CON RASTRO IMPECABLE
        public async Task EliminarAsync(int id, int usuarioId, string ip)
        {
            var factura = await ObtenerPorIdAsync(id);
            if (factura != null)
            {
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
            }
        }

        public bool Existe(int id)
        {
            return _context.Facturas.Any(e => e.Id == id);
        }

        public async Task<List<Cita>> ObtenerCitasDisponiblesAsync()
        {
            return await _context.Citas.ToListAsync();
        }

        public async Task<List<Tenant>> ObtenerTenantsAsync()
        {
            return await _context.Tenants.ToListAsync();
        }
    }
}