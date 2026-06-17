using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;

namespace SaaSBarberia.Services
{
    public class BarberoService
    {
        private readonly BarberSaaSDbContext _context;

        public BarberoService(BarberSaaSDbContext context)
        {
            _context = context;
        }

        public async Task<List<Barbero>> ObtenerTodosConRelacionesAsync()
        {
            return await _context.Barberos
                .Include(b => b.Usuario)
                .Include(b => b.Tenant)
                .ToListAsync();
        }

        public async Task<Barbero?> ObtenerPorIdAsync(int id)
        {
            return await _context.Barberos
                .Include(b => b.Usuario)
                .Include(b => b.Tenant)
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task GuardarAsync(Barbero barbero)
        {
            if (barbero.Id == 0)
            {
                _context.Add(barbero);
            }
            else
            {
                _context.Update(barbero);
            }
            await _context.SaveChangesAsync();
        }

        public async Task EliminarAsync(int id)
        {
            var barbero = await _context.Barberos.FindAsync(id);
            if (barbero != null)
            {
                _context.Barberos.Remove(barbero);
                await _context.SaveChangesAsync();
            }
        }

        public bool Existe(int id)
        {
            return _context.Barberos.Any(e => e.Id == id);
        }

        public async Task<List<Usuario>> ObtenerUsuariosDisponiblesAsync()
        {
            // Trae los usuarios para asociarlos al perfil de barbero
            return await _context.Usuarios.ToListAsync();
        }

        public async Task<List<Tenant>> ObtenerTenantsAsync()
        {
            return await _context.Tenants.ToListAsync();
        }
    }
}