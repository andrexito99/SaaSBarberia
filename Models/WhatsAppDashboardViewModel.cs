namespace SaaSBarberia.Models;

public class WhatsAppDashboardViewModel
{
    public string EstadoConexion { get; set; } = "No conectado";
    public string? NumeroWhatsApp { get; set; }
    public int MensajesEnviadosHoy { get; set; }
    public int RecordatoriosEnviados { get; set; }
    public int ConfirmacionesEnviadas { get; set; }
    public int CancelacionesEnviadas { get; set; }
    public int ReprogramacionesEnviadas { get; set; }
    public List<WhatsAppMensajeHistorial> UltimosMensajes { get; set; } = [];
    public List<WhatsAppMensajeHistorial> ErroresRecientes { get; set; } = [];
}
