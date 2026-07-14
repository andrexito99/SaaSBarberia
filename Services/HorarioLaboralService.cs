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

        public async Task<List<HorarioLaboral>> ObtenerHorarioSemanaAsync(int tenantId)
        {
            var horarios = await _context.HorariosLaborales
                .Where(h => h.TenantId == tenantId)
                .OrderBy(h => h.DiaSemana)
                .ToListAsync();

            for (int i = 0; i <= 6; i++)
            {
                if (!horarios.Any(h => h.DiaSemana == i))
                {
                    var horarioNuevo = GenerarHorarioDefectoPorDia(tenantId, i);
                    _context.HorariosLaborales.Add(horarioNuevo);
                    horarios.Add(horarioNuevo);
                }
            }

            await _context.SaveChangesAsync();

            return horarios.OrderBy(h => h.DiaSemana).ToList();
        }

        public async Task<HorarioLaboral> ObtenerHorarioDiaAsync(int tenantId, int diaSemana)
        {
            var horario = await _context.HorariosLaborales
                .FirstOrDefaultAsync(h => h.TenantId == tenantId && h.DiaSemana == diaSemana);

            if (horario == null)
            {
                horario = GenerarHorarioDefectoPorDia(tenantId, diaSemana);
                _context.HorariosLaborales.Add(horario);
                await _context.SaveChangesAsync();
            }

            return horario;
        }

        private HorarioLaboral GenerarHorarioDefectoPorDia(int tenantId, int diaSemana)
        {
            return new HorarioLaboral
            {
                TenantId = tenantId,
                DiaSemana = diaSemana,
                EstaCerrado = (diaSemana == 0),
                HoraInicio = new TimeSpan(8, 0, 0),
                HoraFin = new TimeSpan(12, 0, 0),
                HoraInicio2 = new TimeSpan(14, 0, 0),
                HoraFin2 = new TimeSpan(19, 0, 0)
            };
        }

        public bool ValidarHorario(HorarioLaboral horario)
        {
            if (horario.EstaCerrado) return true;

            if (!horario.HoraInicio.HasValue || !horario.HoraFin.HasValue) return false;
            if (horario.HoraInicio >= horario.HoraFin) return false;

            if (horario.HoraInicio2.HasValue || horario.HoraFin2.HasValue)
            {
                if (!horario.HoraInicio2.HasValue || !horario.HoraFin2.HasValue) return false;
                if (horario.HoraInicio2 >= horario.HoraFin2) return false;
                if (horario.HoraInicio2 <= horario.HoraFin) return false;
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

        public void PrepararHorariosParaGuardar(int tenantId, List<HorarioLaboral> horarios, string selectorJornada)
        {
            foreach (var horario in horarios)
            {
                horario.TenantId = tenantId;

                if (horario.EstaCerrado)
                {
                    horario.HoraInicio = null;
                    horario.HoraFin = null;
                    horario.HoraInicio2 = null;
                    horario.HoraFin2 = null;
                    continue;
                }

                if (selectorJornada != "2")
                {
                    horario.HoraInicio2 = null;
                    horario.HoraFin2 = null;
                }
            }
        }

        public async Task GuardarHorariosSemanaActualizandoAsync(int tenantId, List<HorarioLaboral> horarios, string selectorJornada)
        {
            PrepararHorariosParaGuardar(tenantId, horarios, selectorJornada);

            var existentes = await _context.HorariosLaborales
                .Where(x => x.TenantId == tenantId)
                .ToListAsync();

            for (int dia = 0; dia <= 6; dia++)
            {
                var recibido = horarios.FirstOrDefault(h => h.DiaSemana == dia);
                if (recibido == null)
                {
                    throw new Exception($"No se recibió horario para el día {dia}");
                }

                if (!ValidarHorario(recibido))
                {
                    throw new Exception($"Horario inválido para el día {dia}");
                }

                var existente = existentes.FirstOrDefault(h => h.DiaSemana == dia);
                if (existente == null)
                {
                    recibido.Id = 0;
                    recibido.TenantId = tenantId;
                    _context.HorariosLaborales.Add(recibido);
                    continue;
                }

                existente.EstaCerrado = recibido.EstaCerrado;
                existente.HoraInicio = recibido.HoraInicio;
                existente.HoraFin = recibido.HoraFin;
                existente.HoraInicio2 = recibido.HoraInicio2;
                existente.HoraFin2 = recibido.HoraFin2;
            }

            await _context.SaveChangesAsync();
        }

        public async Task GuardarHorariosSemanaMasivoAsync(int tenantId, List<HorarioLaboral> horarios)
        {
            await GuardarHorariosSemanaActualizandoAsync(tenantId, horarios, "2");
        }
    }
}
