using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;

namespace SaaSBarberia.Services
{
    public interface IHerramientasService
    {
        Task<IEnumerable<Herramienta>> GetAllAsync(int tenantId);
        Task<Herramienta?> GetByIdAsync(int id, int tenantId);
        Task CreateAsync(Herramienta herramienta, int tenantId);
        Task UpdateAsync(Herramienta herramienta, int tenantId);
        Task DeleteAsync(int id, int tenantId);
        bool Exists(int id, int tenantId);
    }

    public class HerramientasService : IHerramientasService
    {
        private readonly BarberSaaSDbContext _context;

        public HerramientasService(BarberSaaSDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Herramienta>> GetAllAsync(int tenantId)
        {
            return await _context.Herramientas
                .Where(h => h.TenantId == tenantId)
                .ToListAsync();
        }

        public async Task<Herramienta?> GetByIdAsync(int id, int tenantId)
        {
            return await _context.Herramientas
                .FirstOrDefaultAsync(h => h.Id == id && h.TenantId == tenantId);
        }

        public async Task CreateAsync(Herramienta herramienta, int tenantId)
        {
            herramienta.TenantId = tenantId; // Forzamos el Tenant del usuario actual
            _context.Add(herramienta);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Herramienta herramienta, int tenantId)
        {
            // Validamos que la herramienta realmente pertenezca al Tenant antes de actualizar
            var existe = await _context.Herramientas
                .AnyAsync(h => h.Id == herramienta.Id && h.TenantId == tenantId);

            if (!existe) throw new UnauthorizedAccessException("No tienes permiso para modificar esta herramienta.");

            herramienta.TenantId = tenantId; // Aseguramos que no saboteen el Tenant
            _context.Entry(herramienta).State = EntityState.Modified;

            // Evitamos que se altere la propiedad de navegación Tenant por error
            _context.Entry(herramienta).Property(x => x.TenantId).IsModified = false;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id, int tenantId)
        {
            var herramienta = await GetByIdAsync(id, tenantId);
            if (herramienta != null)
            {
                _context.Herramientas.Remove(herramienta);
                await _context.SaveChangesAsync();
            }
        }

        public bool Exists(int id, int tenantId)
        {
            return _context.Herramientas.Any(e => e.Id == id && e.TenantId == tenantId);
        }
    }
}