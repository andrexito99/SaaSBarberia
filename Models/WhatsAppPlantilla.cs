using System.ComponentModel.DataAnnotations;

namespace SaaSBarberia.Models;

public class WhatsAppPlantilla
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    [Required, StringLength(50)] public string Tipo { get; set; } = string.Empty;
    [Required, StringLength(4000)] public string Contenido { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;
    public DateTime FechaActualizacion { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}
