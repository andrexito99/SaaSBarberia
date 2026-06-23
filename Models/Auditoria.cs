using System;
using System.ComponentModel.DataAnnotations;

namespace SaaSBarberia.Models
{
    public class Auditoria
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TenantId { get; set; } // De qué barbería es el movimiento

        public int? UsuarioId { get; set; } // Quién lo hizo (null si fue el sistema o anónimo)

        [Required]
        [StringLength(100)]
        public string Entidad { get; set; } // Ejemplo: "Producto", "Cita", "Factura"

        [Required]
        [StringLength(50)]
        public string RegistroId { get; set; } // El ID del elemento afectado (ej: "145")

        [Required]
        [StringLength(50)]
        public string Accion { get; set; } // "Crear", "Editar", "Eliminar"

        [Required]
        public string Descripcion { get; set; } // Ejemplo: "Se editó el precio del Gel fijador de $10K a $15K"

        [Required]
        public DateTime Fecha { get; set; } = DateTime.Now;

        [StringLength(50)]
        public string DireccionIP { get; set; } // IP desde donde se mandó la monda
    }
}