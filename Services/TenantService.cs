using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;

namespace SaaSBarberia.Services
{
    public class TenantService
    {
        private readonly BarberSaaSDbContext _context;

        public TenantService(BarberSaaSDbContext context)
        {
            _context = context;
        }

        public async Task<List<Tenant>> ObtenerTodosAsync()
        {
            return await _context.Tenants.ToListAsync();
        }

        public async Task<Tenant?> ObtenerPorIdAsync(int id)
        {
            return await _context.Tenants.FindAsync(id);
        }

        // 💾 GUARDAR / ACTUALIZAR TENANT (SEDE DE BARBERÍA)
        public async Task GuardarAsync(Tenant tenant)
        {
            if (tenant.Id == 0)
            {
                tenant.FechaRegistro ??= DateTime.Now;
                tenant.Activo ??= true;
                _context.Add(tenant);
            }
            else
            {
                // 🛠️ BLINDAJE ANTI-TRACKING: Buscamos el original traqueado para modificarlo directamente
                var original = await _context.Tenants.FindAsync(tenant.Id);
                if (original != null)
                {
                    // Mapeamos los campos reales de tu modelo Tenant
                    original.NombreNegocio = tenant.NombreNegocio;
                    original.Subdominio = tenant.Subdominio;
                    original.PlanSuscripcion = tenant.PlanSuscripcion;
                    original.CerradoPorEmergencia = tenant.CerradoPorEmergencia;
                    original.FechaUltimoCierre = tenant.FechaUltimoCierre;

                    // Colores y personalización visual
                    original.ColorPrimario = tenant.ColorPrimario;
                    original.ColorSecundario = tenant.ColorSecundario;
                    original.EstiloSidebar = tenant.EstiloSidebar;

                    // Módulos y tiempos
                    original.TieneModuloHerramientas = tenant.TieneModuloHerramientas;
                    original.TiempoEntreCitas = tenant.TiempoEntreCitas;

                    if (tenant.Activo.HasValue)
                        original.Activo = tenant.Activo;

                    // 📸 LOGO: Reemplaza solo si mandaron un archivo nuevo
                    if (!string.IsNullOrEmpty(tenant.RutaLogo))
                    {
                        original.RutaLogo = tenant.RutaLogo;
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task EliminarAsync(int id)
        {
            var tenant = await _context.Tenants.FindAsync(id);
            if (tenant != null)
            {
                _context.Tenants.Remove(tenant);
                await _context.SaveChangesAsync();
            }
        }

        public bool Existe(int id)
        {
            return _context.Tenants.Any(e => e.Id == id);
        }
    }
}