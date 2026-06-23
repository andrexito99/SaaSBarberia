using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;

namespace SaaSBarberia.Services
{
    public class UsuarioService
    {
        private readonly BarberSaaSDbContext _context;
        private readonly IAuditoriaService _auditoriaService; // 🚨 El ojo de la ley

        public UsuarioService(BarberSaaSDbContext context, IAuditoriaService auditoriaService)
        {
            _context = context;
            _auditoriaService = auditoriaService;
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

        // 💾 GUARDAR / ACTUALIZAR USUARIO CON DETECCIÓN DE CAMBIOS DE SEGURIDAD
        public async Task GuardarAsync(Usuario usuario, int ejecutorId, string ip)
        {
            if (usuario.Id == 0)
            {
                usuario.Activo ??= true;
                usuario.TokenUsado ??= false;

                _context.Add(usuario);
                await _context.SaveChangesAsync();

                // 🔥 AUDITORÍA: Registro de nueva cuenta creada en el sistema
                int tenantLog = usuario.TenantId ?? 0;
                await _auditoriaService.RegistrarAsync(
                    tenantLog,
                    ejecutorId,
                    "Usuario",
                    usuario.Id.ToString(),
                    "Crear",
                    $"Se registró un nuevo usuario: '{usuario.Nombre}' (Email: {usuario.Email}, Rol ID: {usuario.RoleId}).",
                    ip
                );
            }
            else
            {
                // Obtenemos los datos viejos sin tracking para auditar alteraciones críticas
                var existente = await _context.Usuarios
                    .AsNoTracking()
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.Id == usuario.Id);

                if (existente != null)
                {
                    usuario.ResetToken = existente.ResetToken;
                    usuario.TokenExpiracion = existente.TokenExpiracion;
                    usuario.TokenUsado = existente.TokenUsado;
                    if (string.IsNullOrEmpty(usuario.PasswordHash))
                    {
                        usuario.PasswordHash = existente.PasswordHash;
                    }

                    _context.Update(usuario);
                    await _context.SaveChangesAsync();

                    // Generamos los detalles del cambio
                    string cambios = $"Modificación en el usuario '{usuario.Nombre}' (ID: {usuario.Id}). ";
                    bool huboCambioCritico = false;

                    if (existente.RoleId != usuario.RoleId)
                    {
                        cambios += $"¡ALERTA DE SEGURIDAD! Rol cambiado (Rol Anterior ID: {existente.RoleId} -> Nuevo Rol ID: {usuario.RoleId}). ";
                        huboCambioCritico = true;
                    }
                    if (existente.Activo != usuario.Activo)
                    {
                        cambios += $"Estado de cuenta alterado: (Activo anterior: {existente.Activo} -> Nuevo: {usuario.Activo}). ";
                        huboCambioCritico = true;
                    }

                    // Si cambiaron nombre, email o teléfono también se deja registro corto si no es crítico
                    if (!huboCambioCritico) cambios += "Actualización de datos personales básicos.";

                    int tenantLog = usuario.TenantId ?? existente.TenantId ?? 0;
                    await _auditoriaService.RegistrarAsync(tenantLog, ejecutorId, "Usuario", usuario.Id.ToString(), "Editar", cambios, ip);
                }
            }
        }

        public async Task EliminarAsync(int id, int ejecutorId, string ip)
        {
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario != null)
            {
                int tenantLog = usuario.TenantId ?? 0;
                string detalle = $"Cuenta ELIMINADA por completo. Nombre: '{usuario.Nombre}', Email: '{usuario.Email}', Rol ID: {usuario.RoleId}.";

                _context.Usuarios.Remove(usuario);
                await _context.SaveChangesAsync();

                // 🔥 AUDITORÍA: El rastro imborrable del usuario borrado
                await _auditoriaService.RegistrarAsync(tenantLog, ejecutorId, "Usuario", id.ToString(), "Eliminar", detalle, ip);
            }
        }

        public bool Existe(int id)
        {
            return _context.Usuarios.Any(e => e.Id == id);
        }

        public async Task<List<Role>> ObtenerRolesAsync() => await _context.Roles.ToListAsync();
        public async Task<List<Tenant>> ObtenerTenantsAsync() => await _context.Tenants.ToListAsync();

        public async Task<bool> ExisteEmailAsync(string email) => await _context.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == email);
        public async Task<bool> ExisteTelefonoAsync(string telefono) => await _context.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Telefono == telefono);
        public async Task<bool> ExisteEmailParaOtroUsuarioAsync(string email, int usuarioId) => await _context.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == email && u.Id != usuarioId);
        public async Task<bool> ExisteTelefonoParaOtroUsuarioAsync(string telefono, int usuarioId) => await _context.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Telefono == telefono && u.Id != usuarioId);
    }
}