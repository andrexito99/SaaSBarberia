using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;

namespace SaaSBarberia.Services
{
    public class TenantService
    {
        private readonly BarberSaaSDbContext _context;

        public TenantService(BarberSaaSDbContext context)
        {
            _context = context;
        }

        public async Task<List<Tenant>> ObtenerTodosAsync()
        {
            return await _context.Tenants.ToListAsync();
        }

        public async Task<Tenant?> ObtenerPorIdAsync(int id)
        {
            return await _context.Tenants.FindAsync(id);
        }

        public async Task GuardarAsync(Tenant tenant)
        {
            if (tenant.Id == 0)
            {
                tenant.FechaRegistro ??= System.DateTime.Now;
                tenant.Activo ??= true;
                _context.Add(tenant);
            }
            else
            {
                _context.Update(tenant);
            }
            await _context.SaveChangesAsync();
        }

        public async Task EliminarAsync(int id)
        {
            var tenant = await _context.Tenants.FindAsync(id);
            if (tenant != null)
            {
                _context.Tenants.Remove(tenant);
                await _context.SaveChangesAsync();
            }
        }

        public bool Existe(int id)
        {
            return _context.Tenants.Any(e => e.Id == id);
        }
    }
}