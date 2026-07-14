using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks; // Añadido para soporte asíncrono si lo requieres
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data; // Asegúrate de tener tu DbContext aquí
using SaaSBarberia.Models;

namespace SaaSBarberia.Services
{
    public class AgendaInteligenteService
    {
        private readonly BarberSaaSDbContext _context;
        private const int IntervaloSlotMinutos = 30;

        // Inyectamos el DbContext para poder validar las ausencias y citas directamente cuando se necesite
        public AgendaInteligenteService(BarberSaaSDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// MÉTODOS NUEVOS: Para soporte del controlador de ausencias
        /// </summary>
        public async Task<bool> TieneCitasEnRangoAsync(int tenantId, int barberoId, DateTime inicio, DateTime fin)
        {
            var citas = await _context.Citas
                .Include(c => c.Servicio)
                .Where(c => c.TenantId == tenantId && c.BarberoId == barberoId && c.Estado != "Cancelada" && c.FechaHora < fin)
                .ToListAsync();
            return citas.Any(c => c.FechaHora.AddMinutes(c.Servicio?.DuracionMinutos ?? IntervaloSlotMinutos) > inicio);
        }

        public Task<bool> TieneAusenciaEnRangoAsync(int tenantId, int barberoId, DateTime inicio, DateTime fin) =>
            _context.BarberoAusencias.AnyAsync(a => a.TenantId == tenantId && a.BarberoId == barberoId && a.Activo && a.FechaInicio < fin && a.FechaFin > inicio);

        /// <summary>
        /// TU MÉTODO ORIGINAL: Obtener horas disponibles, ahora cruzando también con las ausencias de la BD
        /// </summary>
        public List<DateTime> ObtenerHorasDisponibles(
            List<Cita> citasExistentes,
            List<BarberoAusencia> ausenciasActivas, // <-- NUEVO: Le pasamos las ausencias del barbero para ese día
            DateTime fechaConsultada,
            int duracionTotalRequerida,
            HorarioLaboral horario,
            int tiempoEntreCitasMinutos = 0)
        {
            var horasDisponibles = new List<DateTime>();

            if (horario == null || horario.EstaCerrado) return horasDisponibles;

            // 1. Evaluar Primera Jornada (Mañana)
            if (horario.HoraInicio.HasValue && horario.HoraFin.HasValue)
            {
                GenerarBloquesParaJornada(
                    horasDisponibles, citasExistentes, ausenciasActivas, fechaConsultada,
                    duracionTotalRequerida, tiempoEntreCitasMinutos, horario.HoraInicio.Value, horario.HoraFin.Value
                );
            }

            // 2. Evaluar Segunda Jornada (Tarde - Si maneja jornada partida)
            if (horario.HoraInicio2.HasValue && horario.HoraFin2.HasValue)
            {
                GenerarBloquesParaJornada(
                    horasDisponibles, citasExistentes, ausenciasActivas, fechaConsultada,
                    duracionTotalRequerida, tiempoEntreCitasMinutos, horario.HoraInicio2.Value, horario.HoraFin2.Value
                );
            }

            return horasDisponibles.OrderBy(h => h).ToList();
        }

        /// <summary>
        /// TU MÉTODO AUXILIAR ORIGINAL: Modificado para tumbar slots que se crucen con ausencias
        /// </summary>
        private void GenerarBloquesParaJornada(
            List<DateTime> horasDisponibles,
            List<Cita> citasExistentes,
            List<BarberoAusencia> ausenciasActivas, // <-- NUEVO
            DateTime fechaConsultada,
            int duracionTotalRequerida,
            int tiempoEntreCitasMinutos,
            TimeSpan horaInicioJornada,
            TimeSpan horaFinJornada)
        {
            DateTime inicioDia = fechaConsultada.Date.Add(horaInicioJornada);
            DateTime finDia = fechaConsultada.Date.Add(horaFinJornada);

            // Si es hoy, no dejamos reservar horas pasadas
            if (fechaConsultada.Date == DateTime.Today)
            {
                var ahora = DateTime.Now;
                if (inicioDia < ahora)
                {
                    int minutosRestantes = IntervaloSlotMinutos - (ahora.Minute % IntervaloSlotMinutos);
                    inicioDia = ahora.AddMinutes(minutosRestantes).AddSeconds(-ahora.Second).AddMilliseconds(-ahora.Millisecond);
                }
            }

            DateTime slotEvaluar = inicioDia;
            int duracionConMargen = duracionTotalRequerida + Math.Max(0, tiempoEntreCitasMinutos);

            while (slotEvaluar.AddMinutes(duracionTotalRequerida) <= finDia)
            {
                DateTime horaFinServicioConMargen = slotEvaluar.AddMinutes(duracionConMargen);

                // 1. Validar contra las citas agendadas activas (Tu lógica original intacta)
                bool estaOcupadoPorCita = citasExistentes.Any(cita =>
                {
                    int duracionExistente = cita.Servicio?.DuracionMinutos ?? IntervaloSlotMinutos;
                    DateTime inicioExistente = cita.FechaHora;
                    DateTime finExistenteConMargen = cita.FechaHora.AddMinutes(duracionExistente + Math.Max(0, tiempoEntreCitasMinutos));

                    return
                    cita.Estado != "Cancelada" &&
                    slotEvaluar < finExistenteConMargen &&
                    horaFinServicioConMargen > inicioExistente;
                });

                // 2. NUEVO: Validar contra las ausencias activas del barbero
                bool estaOcupadoPorAusencia = ausenciasActivas.Any(ausencia =>
                    ausencia.Activo &&
                    slotEvaluar < ausencia.FechaFin &&
                    horaFinServicioConMargen > ausencia.FechaInicio
                );

                // Solo si el slot no está pisado por cita ni por ausencia, se añade
                if (!estaOcupadoPorCita && !estaOcupadoPorAusencia)
                {
                    horasDisponibles.Add(slotEvaluar);
                }

                slotEvaluar = slotEvaluar.AddMinutes(IntervaloSlotMinutos);
            }
        }
    }
}
