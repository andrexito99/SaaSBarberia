namespace SaaSBarberia.Models;

public class WhatsAppMensajeHistorial
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int? CitaId { get; set; }
    public int? ClienteId { get; set; }
    public int? BarberoId { get; set; }
    public string Telefono { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? Error { get; set; }
    public string? MetaMessageId { get; set; }
    public string? RespuestaMeta { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual Cita? Cita { get; set; }
    public virtual Usuario? Cliente { get; set; }
    public virtual Barbero? Barbero { get; set; }
}
