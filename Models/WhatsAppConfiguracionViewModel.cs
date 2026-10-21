using System.ComponentModel.DataAnnotations;

namespace SaaSBarberia.Models;

public class WhatsAppConfiguracionViewModel
{
    [StringLength(30)] public string NumeroWhatsApp { get; set; } = string.Empty;
    [StringLength(100)] public string PhoneNumberId { get; set; } = string.Empty;
    [StringLength(100)] public string BusinessAccountId { get; set; } = string.Empty;
    [DataType(DataType.Password), StringLength(2000)] public string? NuevoAccessToken { get; set; }
    public bool ConexionActiva { get; set; }
    public string EstadoConexion { get; set; } = "No conectado";
    public DateTime? FechaConexion { get; set; }
    public bool EnviarConfirmaciones { get; set; } = true;
    public bool EnviarRecordatorio24Horas { get; set; } = true;
    public bool EnviarRecordatorio2Horas { get; set; } = true;
    public bool EnviarCancelaciones { get; set; } = true;
    public bool EnviarReprogramaciones { get; set; } = true;
    public bool PermitirMensajesPersonalizados { get; set; } = true;
}
