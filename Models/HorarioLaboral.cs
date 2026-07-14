using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SaaSBarberia.Models
{
    public partial class HorarioLaboral
    {
        public int Id { get; set; }
        public int TenantId { get; set; }

        [Range(0, 6)]
        public int DiaSemana { get; set; } // 0=Domingo, 1=Lunes, etc.

        // Primera jornada (Ej: 8:00 AM - 12:00 PM)
        public TimeSpan? HoraInicio { get; set; }
        public TimeSpan? HoraFin { get; set; }

        // Segunda jornada por si parten el día para el almuerzo (Ej: 2:00 PM - 7:00 PM)
        public TimeSpan? HoraInicio2 { get; set; }
        public TimeSpan? HoraFin2 { get; set; }

        public bool EstaCerrado { get; set; }

        [ValidateNever]
        public virtual Tenant Tenant { get; set; } = null!;
    }
}