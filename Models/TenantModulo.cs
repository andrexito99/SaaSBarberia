using System.ComponentModel.DataAnnotations;

namespace SaaSBarberia.Models;

/// <summary>Habilitación de funcionalidades por tenant. El código es estable y no depende del menú.</summary>
public class TenantModulo
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    [Required, StringLength(50)] public string Codigo { get; set; } = string.Empty;
    public bool Habilitado { get; set; }
    public DateTime FechaActualizacion { get; set; }
    public Tenant Tenant { get; set; } = null!;
}
