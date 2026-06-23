using System;
using System.Threading.Tasks;
using SaaSBarberia.Data;
using SaaSBarberia.Models;

namespace SaaSBarberia.Services
{
    public class AuditoriaService : IAuditoriaService
    {
        private readonly BarberSaaSDbContext _context;

        public AuditoriaService(BarberSaaSDbContext context)
        {
            _context = context;
        }

        public async Task RegistrarAsync(int tenantId, int? usuarioId, string entidad, string registroId, string accion, string descripcion, string ip)
        {
            var auditoria = new Auditoria
            {
                TenantId = tenantId,
                UsuarioId = usuarioId,
                Entidad = entidad,
                RegistroId = registroId,
                Accion = accion,
                Descripcion = descripcion,
                Fecha = DateTime.Now,
                DireccionIP = string.IsNullOrEmpty(ip) ? "127.0.0.1" : ip
            };

            _context.Auditorias.Add(auditoria);
            await _context.SaveChangesAsync();
        }
    }
}