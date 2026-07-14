using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SaaSBarberia.Models
{
    public class BarberoAusencia
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TenantId { get; set; }

        [Required]
        public int BarberoId { get; set; }

        [Required]
        [Display(Name = "Fecha y Hora de Inicio")]
        public DateTime FechaInicio { get; set; }

        [Required]
        [Display(Name = "Fecha y Hora de Fin")]
        public DateTime FechaFin { get; set; }

        [Required]
        [Display(Name = "Tipo de Ausencia")]
        public TipoAusencia Tipo { get; set; }

        [Required]
        [StringLength(250, ErrorMessage = "El motivo no puede superar los 250 caracteres.")]
        public string Motivo { get; set; } = string.Empty;

        public string? Observaciones { get; set; }

        [Required]
        public string CreadoPorUsuarioId { get; set; } = string.Empty;

        public bool Activo { get; set; } = true;

        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        // Propiedad de navegación
        [ForeignKey("BarberoId")]
        public virtual Barbero? Barbero { get; set; }
    }
}