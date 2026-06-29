using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Models;

namespace SaaSBarberia.Services
{
    public class AgendaInteligenteService
    {
        private const int IntervaloSlotMinutos = 30;

        public List<DateTime> ObtenerHorasDisponibles(
            List<Cita> citasExistentes,
            DateTime fechaConsultada,
            int duracionTotalRequerida,
            HorarioLaboral horario)
        {
            var horasDisponibles = new List<DateTime>();

            if (horario == null || horario.EstaCerrado) return horasDisponibles;

            // 1. Evaluar Primera Jornada (Mañana)
            if (horario.HoraInicio.HasValue && horario.HoraFin.HasValue)
            {
                GenerarBloquesParaJornada(
                    horasDisponibles, citasExistentes, fechaConsultada,
                    duracionTotalRequerida, horario.HoraInicio.Value, horario.HoraFin.Value
                );
            }

            // 2. Evaluar Segunda Jornada (Tarde - Si maneja jornada partida)
            if (horario.HoraInicio2.HasValue && horario.HoraFin2.HasValue)
            {
                GenerarBloquesParaJornada(
                    horasDisponibles, citasExistentes, fechaConsultada,
                    duracionTotalRequerida, horario.HoraInicio2.Value, horario.HoraFin2.Value
                );
            }

            return horasDisponibles.OrderBy(h => h).ToList();
        }

        private void GenerarBloquesParaJornada(
            List<DateTime> horasDisponibles,
            List<Cita> citasExistentes,
            DateTime fechaConsultada,
            int duracionTotalRequerida,
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

            while (slotEvaluar.AddMinutes(duracionTotalRequerida) <= finDia)
            {
                DateTime horaFinServicio = slotEvaluar.AddMinutes(duracionTotalRequerida);

                // Validar contra las citas agendadas activas
                bool estaOcupado = citasExistentes.Any(cita =>
                    cita.Estado != "Cancelada" &&
                    slotEvaluar < cita.FechaHora.AddMinutes(cita.Servicio.DuracionMinutos) &&
                    horaFinServicio > cita.FechaHora
                );

                if (!estaOcupado)
                {
                    horasDisponibles.Add(slotEvaluar);
                }

                slotEvaluar = slotEvaluar.AddMinutes(IntervaloSlotMinutos);
            }
        }
    }
}