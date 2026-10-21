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
        private readonly IAuditoriaService _auditoriaService;
        private readonly IEventosSistemaService _eventos;

        public UsuarioService(BarberSaaSDbContext context, IAuditoriaService auditoriaService, IEventosSistemaService eventos)
        {
            _context = context;
            _auditoriaService = auditoriaService;
            _eventos = eventos;
        }

        public async Task<List<Usuario>> ObtenerTodosConRelacionesAsync(int? tenantActual, bool esSuperAdmin)
        {
            if (!esSuperAdmin && !tenantActual.HasValue) return new List<Usuario>();

            return await _context.Usuarios
                .Where(u => esSuperAdmin || u.TenantId == tenantActual)
                .Include(u => u.Role)
                .Include(u => u.Tenant)
                .ToListAsync();
        }

        public async Task<Usuario?> ObtenerPorIdAsync(int id, int? tenantActual, bool esSuperAdmin)
        {
            if (!esSuperAdmin && !tenantActual.HasValue) return null;

            return await _context.Usuarios
                .Include(u => u.Role)
                .Include(u => u.Tenant)
                .FirstOrDefaultAsync(u => u.Id == id && (esSuperAdmin || u.TenantId == tenantActual));
        }

        public async Task CrearAsync(Usuario usuario, int ejecutorId, string ip, int? tenantActual, bool esSuperAdmin)
        {
            if (!esSuperAdmin)
            {
                if (!tenantActual.HasValue || await EsRolSuperAdminAsync(usuario.RoleId))
                    throw new UnauthorizedAccessException();

                // No se utiliza el TenantId recibido del cliente para Administrador de barberia.
                usuario.TenantId = tenantActual;
            }

            usuario.Activo ??= true;
            usuario.TokenUsado ??= false;
            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            int tenantLog = usuario.TenantId ?? 0;
            await _auditoriaService.RegistrarAsync(
                tenantLog,
                ejecutorId,
                "Usuario",
                usuario.Id.ToString(),
                "Crear",
                $"Se registró un nuevo usuario: '{usuario.Nombre}' (Email: {usuario.Email}, Rol ID: {usuario.RoleId}).",
                ip);

            if (usuario.TenantId.HasValue)
                await _eventos.PublicarAsync(new EventoSistema(TipoEventoSistema.NuevoUsuario, usuario.TenantId.Value, "Nuevo usuario", $"Se creó el usuario {usuario.Nombre}.", "/Usuarios/Details/" + usuario.Id));
        }

        public async Task<bool> ActualizarAsync(Usuario usuario, int ejecutorId, string ip, int? tenantActual, bool esSuperAdmin)
        {
            if (!esSuperAdmin && !tenantActual.HasValue) throw new UnauthorizedAccessException();

            // El predicado de tenant es explícito y complementa el global query filter.
            var existente = await _context.Usuarios
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == usuario.Id && (esSuperAdmin || u.TenantId == tenantActual));
            if (existente == null) return false;

            if (!esSuperAdmin && await EsRolSuperAdminAsync(usuario.RoleId))
                throw new UnauthorizedAccessException();
            if (!esSuperAdmin && usuario.Id == ejecutorId && existente.RoleId != usuario.RoleId)
                throw new UnauthorizedAccessException();

            var roleIdAnterior = existente.RoleId;
            var activoAnterior = existente.Activo;

            // Solo se copian campos editables; TenantId y tokens nunca se enlazan ni se actualizan.
            existente.Nombre = usuario.Nombre;
            existente.Email = usuario.Email;
            existente.Telefono = usuario.Telefono;
            existente.Activo = usuario.Activo;
            existente.RoleId = usuario.RoleId;
            if (esSuperAdmin)
                existente.TenantId = usuario.TenantId;
            if (!string.IsNullOrEmpty(usuario.PasswordHash))
                existente.PasswordHash = usuario.PasswordHash;

            await _context.SaveChangesAsync();

            string cambios = $"Modificación en el usuario '{existente.Nombre}' (ID: {existente.Id}). ";
            bool huboCambioCritico = false;
            if (roleIdAnterior != existente.RoleId)
            {
                cambios += $"¡ALERTA DE SEGURIDAD! Rol cambiado (Rol Anterior ID: {roleIdAnterior} -> Nuevo Rol ID: {existente.RoleId}). ";
                huboCambioCritico = true;
            }
            if (activoAnterior != existente.Activo)
            {
                cambios += $"Estado de cuenta alterado: (Activo anterior: {activoAnterior} -> Nuevo: {existente.Activo}). ";
                huboCambioCritico = true;
            }
            if (!huboCambioCritico) cambios += "Actualización de datos personales básicos.";

            await _auditoriaService.RegistrarAsync(existente.TenantId ?? 0, ejecutorId, "Usuario", existente.Id.ToString(), "Editar", cambios, ip);
            return true;
        }

        public async Task<bool> EliminarAsync(int id, int ejecutorId, string ip, int? tenantActual, bool esSuperAdmin)
        {
            if (!esSuperAdmin && !tenantActual.HasValue) return false;

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Id == id && (esSuperAdmin || u.TenantId == tenantActual));
            if (usuario == null) return false;

            int tenantLog = usuario.TenantId ?? 0;
            string detalle = $"Cuenta ELIMINADA por completo. Nombre: '{usuario.Nombre}', Email: '{usuario.Email}', Rol ID: {usuario.RoleId}.";
            _context.Usuarios.Remove(usuario);
            await _context.SaveChangesAsync();
            await _auditoriaService.RegistrarAsync(tenantLog, ejecutorId, "Usuario", id.ToString(), "Eliminar", detalle, ip);
            return true;
        }

        public bool Existe(int id) => _context.Usuarios.Any(e => e.Id == id);

        public Task<bool> EsRolSuperAdminAsync(int roleId) =>
            _context.Roles.AnyAsync(r => r.Id == roleId && r.NombreRole == "SuperAdmin");

        public async Task<List<Role>> ObtenerRolesAsync(bool esSuperAdmin) =>
            await _context.Roles.Where(r => esSuperAdmin || r.NombreRole != "SuperAdmin").ToListAsync();

        public async Task<List<Tenant>> ObtenerTenantsAsync(int? tenantActual, bool esSuperAdmin)
        {
            if (!esSuperAdmin && !tenantActual.HasValue) return new List<Tenant>();
            return await _context.Tenants.Where(t => esSuperAdmin || t.Id == tenantActual).ToListAsync();
        }

        public async Task<bool> ExisteEmailAsync(string email) => await _context.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == email);
        public async Task<bool> ExisteTelefonoAsync(string telefono) => await _context.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Telefono == telefono);
        public async Task<bool> ExisteEmailParaOtroUsuarioAsync(string email, int usuarioId) => await _context.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == email && u.Id != usuarioId);
        public async Task<bool> ExisteTelefonoParaOtroUsuarioAsync(string telefono, int usuarioId) => await _context.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Telefono == telefono && u.Id != usuarioId);
    }
}
