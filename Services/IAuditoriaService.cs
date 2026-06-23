using System.Threading.Tasks;

namespace SaaSBarberia.Services
{
    public interface IAuditoriaService
    {
        Task RegistrarAsync(int tenantId, int? usuarioId, string entidad, string registroId, string accion, string descripcion, string ip);
    }
}