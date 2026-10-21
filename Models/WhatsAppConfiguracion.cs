using System.ComponentModel.DataAnnotations;

namespace SaaSBarberia.Models;

public class WhatsAppConfiguracion
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    [Required, StringLength(30)] public string NumeroWhatsApp { get; set; } = string.Empty;
    [Required, StringLength(100)] public string PhoneNumberId { get; set; } = string.Empty;
    [Required, StringLength(100)] public string BusinessAccountId { get; set; } = string.Empty;
    [Required, StringLength(4000)] public string AccessTokenProtegido { get; set; } = string.Empty;
    public bool ConexionActiva { get; set; }
    [StringLength(20)] public string EstadoConexion { get; set; } = "No conectado";
    public DateTime? FechaConexion { get; set; }
    [StringLength(4000)] public string? WebhookVerifyTokenProtegido { get; set; }
    public bool EnviarConfirmaciones { get; set; } = true;
    public bool EnviarRecordatorio24Horas { get; set; } = true;
    public bool EnviarRecordatorio2Horas { get; set; } = true;
    public bool EnviarCancelaciones { get; set; } = true;
    public bool EnviarReprogramaciones { get; set; } = true;
    public bool PermitirMensajesPersonalizados { get; set; } = true;
    public virtual Tenant Tenant { get; set; } = null!;
}
