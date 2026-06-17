using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;

namespace SaaSBarberia.Services
{
    public class FacturaService
    {
        private readonly BarberSaaSDbContext _context;

        public FacturaService(BarberSaaSDbContext context)
        {
            _context = context;
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

        public async Task GuardarAsync(Factura factura)
        {
            if (factura.Id == 0)
            {
                _context.Add(factura);
            }
            else
            {
                _context.Update(factura);
            }
            await _context.SaveChangesAsync();
        }

        public async Task EliminarAsync(int id)
        {
            var factura = await _context.Facturas.FindAsync(id);
            if (factura != null)
            {
                _context.Facturas.Remove(factura);
                await _context.SaveChangesAsync();
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