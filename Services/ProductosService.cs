using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;

namespace SaaSBarberia.Services
{
    public interface IProductosService
    {
        Task<IEnumerable<Producto>> GetAllAsync(int tenantId);
        Task<Producto?> GetByIdAsync(int id, int tenantId);
        Task CreateAsync(Producto producto, int tenantId);
        Task UpdateAsync(Producto producto, int tenantId);
        Task DeleteAsync(int id, int tenantId);
        bool Exists(int id, int tenantId);
    }

    public class ProductosService : IProductosService
    {
        private readonly BarberSaaSDbContext _context;

        public ProductosService(BarberSaaSDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Producto>> GetAllAsync(int tenantId)
        {
            return await _context.Producto
                .Where(p => p.TenantId == tenantId)
                .ToListAsync();
        }

        public async Task<Producto?> GetByIdAsync(int id, int tenantId)
        {
            return await _context.Producto
                .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
        }

        public async Task CreateAsync(Producto producto, int tenantId)
        {
            producto.TenantId = tenantId; // Clavamos el Tenant actual por seguridad
            _context.Add(producto);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Producto producto, int tenantId)
        {
            var existe = await _context.Producto
                .AnyAsync(p => p.Id == producto.Id && p.TenantId == tenantId);

            if (!existe) throw new UnauthorizedAccessException("No tienes permiso para modificar este producto.");

            producto.TenantId = tenantId;
            _context.Entry(producto).State = EntityState.Modified;
            _context.Entry(producto).Property(x => x.TenantId).IsModified = false; // Evitamos alteraciones del Tenant

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id, int tenantId)
        {
            var producto = await GetByIdAsync(id, tenantId);
            if (producto != null)
            {
                _context.Producto.Remove(producto);
                await _context.SaveChangesAsync();
            }
        }

        public bool Exists(int id, int tenantId)
        {
            return _context.Producto.Any(e => e.Id == id && e.TenantId == tenantId);
        }
    }
}