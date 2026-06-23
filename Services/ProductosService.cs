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
        Task CreateAsync(Producto producto, int tenantId, int usuarioId, string ip);
        Task UpdateAsync(Producto producto, int tenantId, int usuarioId, string ip);
        Task DeleteAsync(int id, int tenantId, int usuarioId, string ip);
        bool Exists(int id, int tenantId);
    }

    public class ProductosService : IProductosService
    {
        private readonly BarberSaaSDbContext _context;
        private readonly IAuditoriaService _auditoriaService; // 🚨 El vigilante inyectado

        public ProductosService(BarberSaaSDbContext context, IAuditoriaService auditoriaService)
        {
            _context = context;
            _auditoriaService = auditoriaService;
        }

        public async Task<IEnumerable<Producto>> GetAllAsync(int tenantId)
        {
            return await _context.Productos
                .Where(p => p.TenantId == tenantId)
                .ToListAsync();
        }

        public async Task<Producto?> GetByIdAsync(int id, int tenantId)
        {
            return await _context.Productos
                .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
        }

        public async Task CreateAsync(Producto producto, int tenantId, int usuarioId, string ip)
        {
            producto.TenantId = tenantId;
            _context.Add(producto);
            await _context.SaveChangesAsync();

            // 🔥 AUDITORÍA: Registro de creación
            await _auditoriaService.RegistrarAsync(
                tenantId,
                usuarioId,
                "Producto",
                producto.Id.ToString(),
                "Crear",
                $"Se creó el producto '{producto.Nombre}' con precio de Venta: ${producto.PrecioVenta.ToString("N2")} y stock inicial: {producto.Stock} unds.",
                ip
            );
        }

        public async Task UpdateAsync(Producto producto, int tenantId, int usuarioId, string ip)
        {
            // Traemos el original sin traqueo para poder comparar qué cambió exactamente
            var original = await _context.Productos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == producto.Id && p.TenantId == tenantId);

            if (original == null) throw new UnauthorizedAccessException("No tienes permiso para modificar este producto o no existe.");

            producto.TenantId = tenantId;
            _context.Entry(producto).State = EntityState.Modified;
            _context.Entry(producto).Property(x => x.TenantId).IsModified = false;

            await _context.SaveChangesAsync();

            // 🔥 AUDITORÍA: Detectamos qué monda cambió para guardar un reporte limpio
            string cambios = $"Se modificó el producto ID {producto.Id}. ";
            if (original.Nombre != producto.Nombre) cambios += $"Nombre: '{original.Nombre}' -> '{producto.Nombre}'. ";
            if (original.Descripcion != producto.Descripcion) cambios += $"Descripción: '{original.Descripcion}' -> '{producto.Descripcion}'. ";
            if (original.PrecioVenta != producto.PrecioVenta) cambios += $"Precio: ${original.PrecioVenta.ToString("N2")} -> ${producto.PrecioVenta.ToString("N2")}. ";
            if (original.Stock != producto.Stock) cambios += $"Stock: {original.Stock} -> {producto.Stock}. ";

            await _auditoriaService.RegistrarAsync(tenantId, usuarioId, "Producto", producto.Id.ToString(), "Editar", cambios, ip);
        }

        public async Task DeleteAsync(int id, int tenantId, int usuarioId, string ip)
        {
            var producto = await GetByIdAsync(id, tenantId);
            if (producto != null)
            {
                _context.Productos.Remove(producto);
                await _context.SaveChangesAsync();

                // 🔥 AUDITORÍA: Registro de eliminación
                await _auditoriaService.RegistrarAsync(
                    tenantId,
                    usuarioId,
                    "Producto",
                    id.ToString(),
                    "Eliminar",
                    $"Se eliminó el producto '{producto.Nombre}' que contaba con {producto.Stock} unds en stock.",
                    ip
                );
            }
        }

        public bool Exists(int id, int tenantId)
        {
            return _context.Productos.Any(e => e.Id == id && e.TenantId == tenantId);
        }
    }
}