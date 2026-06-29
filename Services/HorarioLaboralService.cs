using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;

namespace SaaSBarberia.Services
{
    public class HorarioLaboralService
    {
        private readonly BarberSaaSDbContext _context;

        public HorarioLaboralService(BarberSaaSDbContext context)
        {
            _context = context;
        }

        // Se le agrega tenantId para no listar cosas de otras barberías
        public async Task<List<HorarioLaboral>> ObtenerHorarioSemanaAsync(int tenantId)
        {
            var horarios = await _context.HorariosLaborales
                .Where(h => h.TenantId == tenantId)
                .OrderBy(h => h.DiaSemana)
                .ToListAsync();

            // Si es una barbería vieja y no tiene nada configurado aún, le armamos la semana en memoria
            if (horarios.Count == 0)
            {
                for (int i = 0; i <= 6; i++)
                {
                    horarios.Add(GenerarHorarioDefectoPorDia(tenantId, i));
                }
            }

            return horarios;
        }

        // Recibe el tenantId para saber de qué barbería es el cliente o barbero evaluado
        public async Task<HorarioLaboral> ObtenerHorarioDiaAsync(int tenantId, int diaSemana)
        {
            var horario = await _context.HorariosLaborales
                .FirstOrDefaultAsync(h => h.TenantId == tenantId && h.DiaSemana == diaSemana);

            // 🔥 EL SALVAVIDAS PARA LAS BARBERÍAS VIEJAS: Si retorna null, creamos el horario en memoria
            if (horario == null)
            {
                return GenerarHorarioDefectoPorDia(tenantId, diaSemana);
            }

            return horario;
        }

        // Método auxiliar para no duplicar código del horario por defecto
        private HorarioLaboral GenerarHorarioDefectoPorDia(int tenantId, int diaSemana)
        {
            return new HorarioLaboral
            {
                TenantId = tenantId,
                DiaSemana = diaSemana,
                EstaCerrado = (diaSemana == 0), // Ejemplo: Domingo cerrado por defecto, lunes a sábado abierto
                HoraInicio = new TimeSpan(8, 0, 0),   // 08:00 AM
                HoraFin = new TimeSpan(12, 0, 0),    // 12:00 PM (Almuerzo)
                HoraInicio2 = new TimeSpan(14, 0, 0), // 02:00 PM (Regreso)
                HoraFin2 = new TimeSpan(19, 0, 0)     // 07:00 PM (Cierre)
            };
        }

        public bool ValidarHorario(HorarioLaboral horario)
        {
            if (horario.EstaCerrado) return true;

            if (horario.HoraInicio >= horario.HoraFin) return false;

            if (horario.HoraInicio2.HasValue && horario.HoraFin2.HasValue)
            {
                if (horario.HoraInicio2 >= horario.HoraFin2) return false;
                if (horario.HoraInicio2 < horario.HoraFin) return false;
            }

            return true;
        }

        public async Task GuardarHorariosSemanaAsync(List<HorarioLaboral> horarios)
        {
            foreach (var h in horarios)
            {
                if (!ValidarHorario(h)) throw new Exception($"Horario inválido para el día {h.DiaSemana}");

                var existia = await _context.HorariosLaborales.AnyAsync(x => x.Id == h.Id);
                if (existia) _context.HorariosLaborales.Update(h);
                else _context.HorariosLaborales.Add(h);
            }
            await _context.SaveChangesAsync();
        }
    }
}