using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;
using System.Linq;

namespace SaaSBarberia.Services
{
    public class ServicioService
    {
        private readonly BarberSaaSDbContext _context;

        public ServicioService(BarberSaaSDbContext context)
        {
            _context = context;
        }

        public async Task<List<Servicio>> ObtenerTodosConTenantAsync()
        {
            return await _context.Servicios
                .Include(s => s.Tenant)
                .ToListAsync();
        }

        // 🌟 NUEVO MÉTODO: Trae solo los servicios pertenecientes a una sede específica
        public async Task<List<Servicio>> ObtenerTodosPorTenantAsync(int tenantId)
        {
            return await _context.Servicios
                .Include(s => s.Tenant)
                .Where(s => s.TenantId == tenantId)
                .ToListAsync();
        }

        public async Task<Servicio?> ObtenerPorIdAsync(int id)
        {
            return await _context.Servicios
                .Include(s => s.Tenant)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task GuardarAsync(Servicio servicio)
        {
            if (servicio.Id == 0)
            {
                _context.Add(servicio);
            }
            else
            {
                _context.Update(servicio);
            }
            await _context.SaveChangesAsync();
        }

        public async Task EliminarAsync(int id)
        {
            var servicio = await _context.Servicios.FindAsync(id);
            if (servicio != null)
            {
                _context.Servicios.Remove(servicio);
                await _context.SaveChangesAsync();
            }
        }

        public bool Existe(int id)
        {
            return _context.Servicios.Any(e => e.Id == id);
        }

        public async Task<List<Tenant>> ObtenerTenantsAsync()
        {
            return await _context.Tenants.ToListAsync();
        }
    }
}