using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;

namespace SaaSBarberia.Services
{
    public class BarberoService
    {
        private readonly BarberSaaSDbContext _context;
        private readonly IAuditoriaService _auditoriaService; // 🚨 El vigilante inyectado

        public BarberoService(BarberSaaSDbContext context, IAuditoriaService auditoriaService)
        {
            _context = context;
            _auditoriaService = auditoriaService;
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

        // 💾 GUARDAR / ACTUALIZAR BARBERO CON AUDITORÍA DE COMISIONES
        public async Task GuardarAsync(Barbero barbero, int usuarioId, string ip)
        {
            if (barbero.Id == 0)
            {
                _context.Add(barbero);
                await _context.SaveChangesAsync(); // Guardamos primero para tener el Id asignado

                // Traemos el usuario base para registrar su nombre en el Log
                var usuarioBase = await _context.Usuarios.FindAsync(barbero.UsuarioId);
                string nombreEmpleado = usuarioBase?.Nombre ?? "Desconocido";

                await _auditoriaService.RegistrarAsync(
                    barbero.TenantId,
                    usuarioId,
                    "Barbero",
                    barbero.Id.ToString(),
                    "Crear",
                    $"Se vinculó un nuevo barbero: {nombreEmpleado} (ID Perfil: {barbero.Id}). Especialidad: {barbero.Especialidad}. Comisión asignada: {barbero.ComisionPorcentaje}%.",
                    ip
                );
            }
            else
            {
                // Consultamos el registro anterior sin traqueo para comparar variaciones críticas
                var original = await _context.Barberos
                    .AsNoTracking()
                    .Include(b => b.Usuario)
                    .FirstOrDefaultAsync(b => b.Id == barbero.Id);

                _context.Update(barbero);
                await _context.SaveChangesAsync();

                if (original != null)
                {
                    string nombreEmpleado = original.Usuario?.Nombre ?? "Desconocido";
                    string cambios = $"Se modificó el perfil del barbero {nombreEmpleado} (ID: {barbero.Id}). ";

                    if (original.ComisionPorcentaje != barbero.ComisionPorcentaje)
                        cambios += $"¡PILAS! Comisión Alterada: {original.ComisionPorcentaje}% -> {barbero.ComisionPorcentaje}%. ";
                    if (original.Especialidad != barbero.Especialidad)
                        cambios += $"Especialidad cambiada: '{original.Especialidad}' -> '{barbero.Especialidad}'. ";

                    await _auditoriaService.RegistrarAsync(barbero.TenantId, usuarioId, "Barbero", barbero.Id.ToString(), "Editar", cambios, ip);
                }
            }
        }

        // 🗑️ ELIMINAR REGISTRO DE BARBERO
        public async Task EliminarAsync(int id, int usuarioId, string ip)
        {
            var barbero = await ObtenerPorIdAsync(id);
            if (barbero != null)
            {
                int tenantId = barbero.TenantId;
                string nombreEmpleado = barbero.Usuario?.Nombre ?? "Desconocido";

                _context.Barberos.Remove(barbero);
                await _context.SaveChangesAsync();

                await _auditoriaService.RegistrarAsync(
                    tenantId,
                    usuarioId,
                    "Barbero",
                    id.ToString(),
                    "Eliminar",
                    $"Se ELIMINÓ el perfil laboral del barbero: {nombreEmpleado} (ID Perfil: {id}). Especialidad: {barbero.Especialidad}.",
                    ip
                );
            }
        }

        public bool Existe(int id)
        {
            return _context.Barberos.Any(e => e.Id == id);
        }

        public async Task<List<Usuario>> ObtenerUsuariosDisponiblesAsync()
        {
            return await _context.Usuarios.ToListAsync();
        }

        public async Task<List<Tenant>> ObtenerTenantsAsync()
        {
            return await _context.Tenants.ToListAsync();
        }
    }
}