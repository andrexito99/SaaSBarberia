using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;

namespace SaaSBarberia.Services
{
    public class CitaService
    {
        private readonly BarberSaaSDbContext _context;

        public CitaService(BarberSaaSDbContext context)
        {
            _context = context;
        }

        public async Task<List<Cita>> ObtenerTodasConRelacionesAsync()
        {
            return await _context.Citas
                .Include(c => c.Barbero)
                .Include(c => c.Cliente)
                .Include(c => c.Servicio)
                .Include(c => c.Tenant)
                .ToListAsync();
        }

        public async Task<Cita?> ObtenerPorIdAsync(int id)
        {
            return await _context.Citas
                .Include(c => c.Barbero)
                .Include(c => c.Cliente)
                .Include(c => c.Servicio)
                .Include(c => c.Tenant)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task GuardarAsync(Cita cita)
        {
            if (cita.Id == 0)
            {
                cita.Estado ??= "Pendiente";
                _context.Add(cita);
            }
            else
            {
                _context.Update(cita);
            }
            await _context.SaveChangesAsync();
        }

        // 🌟 CORRECCIÓN INTEGRAL: Eliminación segura con limpieza previa de dependencias
        public async Task EliminarAsync(int id)
        {
            // 1. Buscamos si la bendita cita tiene facturas amarradas en la BD
            var facturasAsociadas = await _context.Set<Factura>() // Usamos Set<Factura>() por si no tienes el DbSet expuesto directamente
                .Where(f => f.CitaId == id)
                .ToListAsync();

            if (facturasAsociadas.Any())
            {
                // 🔥 Machetazo a las facturas primero para limpiar la FK
                _context.Set<Factura>().RemoveRange(facturasAsociadas);
            }

            // 2. Ahora sí, traemos la cita y la borramos sin que el SQL ande chillando
            var cita = await _context.Citas.FindAsync(id);
            if (cita != null)
            {
                _context.Citas.Remove(cita);

                // 💾 Guardamos todo el paquete de cambios en una sola transacción atómica
                await _context.SaveChangesAsync();
            }
        }

        public bool Existe(int id)
        {
            return _context.Citas.Any(e => e.Id == id);
        }

        // Métodos auxiliares para cargar los Selects en los formularios
        public async Task<Usuario?> ObtenerClientePorIdAsync(int id)
        {
            return await _context.Usuarios
                .Include(u => u.Tenant)
                .FirstOrDefaultAsync(u => u.Id == id);
        }
        public async Task<List<Tenant>> ObtenerTenantsAsync() => await _context.Tenants.ToListAsync();
        public async Task<List<Usuario>> ObtenerClientesAsync() => await _context.Usuarios.ToListAsync();
        public async Task<List<Barbero>> ObtenerBarberosAsync()
        {
            return await _context.Barberos
                .Include(b => b.Usuario) // Crucial para que no salga NullReferenceException al mapear el nombre
                .ToListAsync();
        }
        public async Task<List<Servicio>> ObtenerServiciosAsync() => await _context.Servicios.ToListAsync();
    }
}