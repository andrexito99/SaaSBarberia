namespace SaaSBarberia.Models;

public enum TipoEventoSistema
{
    NuevaCita, CitaCancelada, CitaReprogramada, NuevaBarberia, NuevoUsuario,
    NuevoBarbero, NuevaAusencia, Vacaciones, CajaAbierta, CajaCerrada,
    ProductoConPocoInventario, HerramientaAlquilada, HerramientaDevuelta,
    CambioImportanteSistema
}

public sealed record EventoSistema(
    TipoEventoSistema Tipo, int TenantId, string Titulo, string Mensaje,
    string? UrlDestino = null, IReadOnlyCollection<int>? UsuariosDestinoIds = null);
