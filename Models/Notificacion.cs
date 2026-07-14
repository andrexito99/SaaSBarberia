using System.ComponentModel.DataAnnotations;

namespace SaaSBarberia.Models;

public class Notificacion
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public int UsuarioDestinoId { get; set; }
    [Required, StringLength(150)] public string Titulo { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Mensaje { get; set; } = string.Empty;
    [Required, StringLength(50)] public string Tipo { get; set; } = "Informacion";
    [StringLength(80)] public string? Icono { get; set; }
    [StringLength(30)] public string? Color { get; set; }
    public bool Leida { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaLectura { get; set; }
    [StringLength(500)] public string? UrlDestino { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
    public virtual Usuario UsuarioDestino { get; set; } = null!;
}
