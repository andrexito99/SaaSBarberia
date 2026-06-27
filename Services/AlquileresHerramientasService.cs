using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;

namespace SaaSBarberia.Services
{
    public interface IAlquileresHerramientasService
    {
        Task<IEnumerable<AlquilerHerramienta>> GetAllAsync(int tenantId);
        Task<AlquilerHerramienta?> GetByIdAsync(int id, int tenantId);
        Task CreateAsync(AlquilerHerramienta alquiler, int tenantId);
        Task UpdateAsync(AlquilerHerramienta alquiler, int tenantId);
        Task DeleteAsync(int id, int tenantId);
        bool Exists(int id, int tenantId);
        Task<IEnumerable<Herramienta>> GetHerramientasDisponiblesAsync(int tenantId, int? incluirId = null);
        Task<IEnumerable<Barbero>> GetBarberosAsync(int tenantId);
    }

    public class AlquileresHerramientasService : IAlquileresHerramientasService
    {
        private readonly BarberSaaSDbContext _context;

        public AlquileresHerramientasService(BarberSaaSDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<AlquilerHerramienta>> GetAllAsync(int tenantId)
        {
            // 🔥 Corregido a Plural: AlquileresHerramientas
            return await _context.AlquileresHerramientas
                .Include(a => a.Herramienta)
                .Include(a => a.Barbero)
                    .ThenInclude(b => b.Usuario)
                .Where(a => a.TenantId == tenantId)
                .OrderByDescending(a => a.FechaPrestamo)
                .ToListAsync();
        }

        public async Task<AlquilerHerramienta?> GetByIdAsync(int id, int tenantId)
        {
            // 🔥 Corregido a Plural: AlquileresHerramientas
            return await _context.AlquileresHerramientas
                .Include(a => a.Herramienta)
                .Include(a => a.Barbero)
                    .ThenInclude(b => b.Usuario)
                .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId);
        }

        public async Task CreateAsync(AlquilerHerramienta alquiler, int tenantId)
        {
            alquiler.TenantId = tenantId;
            // 🔥 Corregido a Plural: Herramientas
            var herramienta = await _context.Herramientas.FirstOrDefaultAsync(h => h.Id == alquiler.HerramientaId && h.TenantId == tenantId);
            if (herramienta != null)
            {
                herramienta.Estado = "Alquilado";
            }

            _context.Add(alquiler);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(AlquilerHerramienta alquiler, int tenantId)
        {
            // 🔥 Corregido a Plural: AlquileresHerramientas
            var existe = await _context.AlquileresHerramientas.AnyAsync(a => a.Id == alquiler.Id && a.TenantId == tenantId);
            if (!existe) throw new UnauthorizedAccessException("No tienes permiso.");

            alquiler.TenantId = tenantId;
            // 🔥 Corregido a Plural: Herramientas
            var herramienta = await _context.Herramientas.FirstOrDefaultAsync(h => h.Id == alquiler.HerramientaId && h.TenantId == tenantId);
            if (herramienta != null)
            {
                if (alquiler.FechaDevolucion.HasValue)
                {
                    herramienta.Estado = "Disponible";
                }
                else
                {
                    herramienta.Estado = "Alquilado";
                }
            }

            _context.Entry(alquiler).State = EntityState.Modified;
            _context.Entry(alquiler).Property(x => x.TenantId).IsModified = false;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id, int tenantId)
        {
            var alquiler = await GetByIdAsync(id, tenantId);
            if (alquiler != null)
            {
                // 🔥 Corregido a Plural: Herramientas
                var herramienta = await _context.Herramientas.FirstOrDefaultAsync(h => h.Id == alquiler.HerramientaId && h.TenantId == tenantId);
                if (herramienta != null) herramienta.Estado = "Disponible";

                // 🔥 Corregido a Plural: AlquileresHerramientas
                _context.AlquileresHerramientas.Remove(alquiler);
                await _context.SaveChangesAsync();
            }
        }

        public bool Exists(int id, int tenantId)
        {
            // 🔥 Corregido a Plural: AlquileresHerramientas
            return _context.AlquileresHerramientas.Any(e => e.Id == id && e.TenantId == tenantId);
        }

        public async Task<IEnumerable<Herramienta>> GetHerramientasDisponiblesAsync(int tenantId, int? incluirId = null)
        {
            // 🔥 Corregido a Plural: Herramientas
            return await _context.Herramientas
                .Where(h => h.TenantId == tenantId && (h.Estado == "Disponible" || h.Id == incluirId))
                .ToListAsync();
        }

        public async Task<IEnumerable<Barbero>> GetBarberosAsync(int tenantId)
        {
            return await _context.Barberos
                .Include(b => b.Usuario)
                .Where(b => b.TenantId == tenantId)
                .ToListAsync();
        }
    }
}