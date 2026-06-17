using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;

namespace SaaSBarberia.Services
{
    public class UsuarioService
    {
        private readonly BarberSaaSDbContext _context;

        public UsuarioService(BarberSaaSDbContext context)
        {
            _context = context;
        }

        public async Task<List<Usuario>> ObtenerTodosConRelacionesAsync()
        {
            return await _context.Usuarios
                .Include(u => u.Role)
                .Include(u => u.Tenant)
                .ToListAsync();
        }

        public async Task<Usuario?> ObtenerPorIdAsync(int id)
        {
            return await _context.Usuarios
                .Include(u => u.Role)
                .Include(u => u.Tenant)
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task GuardarAsync(Usuario usuario)
        {
            if (usuario.Id == 0)
            {
                usuario.Activo ??= true;
                usuario.TokenUsado ??= false;
                // Aquí a futuro le metes el BCrypt o hash real a la contraseña
                _context.Add(usuario);
            }
            else
            {
                // Para evitar que el Edit borre los tokens si no se envían en el form
                var existente = await _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == usuario.Id);
                if (existente != null)
                {
                    usuario.ResetToken = existente.ResetToken;
                    usuario.TokenExpiracion = existente.TokenExpiracion;
                    usuario.TokenUsado = existente.TokenUsado;
                    if (string.IsNullOrEmpty(usuario.PasswordHash))
                    {
                        usuario.PasswordHash = existente.PasswordHash;
                    }
                }
                _context.Update(usuario);
            }
            await _context.SaveChangesAsync();
        }

        public async Task EliminarAsync(int id)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario != null)
            {
                _context.Usuarios.Remove(usuario);
                await _context.SaveChangesAsync();
            }
        }

        public bool Existe(int id)
        {
            return _context.Usuarios.Any(e => e.Id == id);
        }

        public async Task<List<Role>> ObtenerRolesAsync() => await _context.Roles.ToListAsync();
        public async Task<List<Tenant>> ObtenerTenantsAsync() => await _context.Tenants.ToListAsync();
    }
}