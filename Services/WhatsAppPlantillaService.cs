using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;

namespace SaaSBarberia.Services;

public class WhatsAppPlantillaService
{
    private readonly BarberSaaSDbContext _context;
    public WhatsAppPlantillaService(BarberSaaSDbContext context) => _context = context;

    public async Task CrearPlantillasInicialesSiFaltanAsync(int tenantId)
    {
        var existentes = await _context.WhatsAppPlantillas
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.Tipo)
            .ToListAsync();

        var tiposExistentes = existentes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nuevas = TiposRequeridos
            .Where(tipo => !tiposExistentes.Contains(tipo))
            .Select(tipo => new WhatsAppPlantilla
            {
                TenantId = tenantId,
                Tipo = tipo,
                Contenido = ContenidoPorDefecto(tipo),
                Activa = true,
                FechaActualizacion = DateTime.UtcNow
            })
            .ToList();

        if (nuevas.Count == 0) return;
        _context.WhatsAppPlantillas.AddRange(nuevas);
        await _context.SaveChangesAsync();
    }

    public Task<List<WhatsAppPlantilla>> ObtenerAsync(int tenantId) => _context.WhatsAppPlantillas.Where(p => p.TenantId == tenantId).OrderBy(p => p.Tipo).ToListAsync();
    public Task<WhatsAppPlantilla?> ObtenerAsync(int id, int tenantId) => _context.WhatsAppPlantillas.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

    public async Task GuardarAsync(WhatsAppPlantilla plantilla, int tenantId)
    {
        if (string.IsNullOrWhiteSpace(plantilla.Contenido)) throw new InvalidOperationException("El contenido de la plantilla es obligatorio.");
        var variablesNoPermitidas = System.Text.RegularExpressions.Regex.Matches(plantilla.Contenido, @"\{[^}]+\}")
            .Select(m => m.Value).Except(VariablesPermitidas, StringComparer.Ordinal).ToList();
        if (variablesNoPermitidas.Count > 0) throw new InvalidOperationException($"Variables no soportadas: {string.Join(", ", variablesNoPermitidas)}.");
        var actual = await _context.WhatsAppPlantillas.FirstOrDefaultAsync(p => p.Id == plantilla.Id && p.TenantId == tenantId);
        if (actual == null)
        {
            if (await _context.WhatsAppPlantillas.AnyAsync(p => p.TenantId == tenantId && p.Tipo == plantilla.Tipo)) throw new InvalidOperationException("Ya existe una plantilla para este tipo de mensaje.");
            plantilla.Id = 0; plantilla.TenantId = tenantId; plantilla.FechaActualizacion = DateTime.Now;
            _context.WhatsAppPlantillas.Add(plantilla);
        }
        else
        {
            actual.Contenido = plantilla.Contenido;
            actual.Activa = plantilla.Activa;
            actual.FechaActualizacion = DateTime.Now;
        }
        await _context.SaveChangesAsync();
    }

    public async Task<string> ObtenerContenidoAsync(int tenantId, string tipo) =>
        await _context.WhatsAppPlantillas.IgnoreQueryFilters().Where(p => p.TenantId == tenantId && p.Tipo == tipo && p.Activa).Select(p => p.Contenido).FirstOrDefaultAsync()
        ?? ContenidoPorDefecto(tipo);

    public async Task EliminarAsync(int id, int tenantId)
    {
        var plantilla = await _context.WhatsAppPlantillas.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);
        if (plantilla == null) return;
        _context.WhatsAppPlantillas.Remove(plantilla);
        await _context.SaveChangesAsync();
    }

    public static string ContenidoPorDefecto(string tipo) => tipo switch
    {
        "Confirmacion" => "Hola {Cliente}, tu cita en {Barberia} quedó confirmada para el {Fecha} a las {Hora} con {Barbero}. Servicio: {Servicio}.",
        "Cancelacion" => "Hola {Cliente}, tu cita del {Fecha} a las {Hora} en {Barberia} fue cancelada.",
        "Reprogramacion" => "Hola {Cliente}, tu cita fue reprogramada para el {Fecha} a las {Hora} con {Barbero}. Servicio: {Servicio}.",
        "Recordatorio24Horas" => "Hola {Cliente}, te recordamos tu cita mañana ({Fecha}) a las {Hora} en {Barberia}.",
        "Recordatorio2Horas" => "Hola {Cliente}, tu cita en {Barberia} es en aproximadamente 2 horas, a las {Hora}.",
        "Bienvenida" => "Hola {Cliente}, te damos la bienvenida a {Barberia}.",
        _ => "Hola {Cliente}, te escribimos desde {Barberia}."
    };

    public static readonly string[] TiposRequeridos = ["Confirmacion", "Cancelacion", "Reprogramacion", "Recordatorio24Horas", "Recordatorio2Horas", "Bienvenida"];
    public static readonly string[] VariablesPermitidas = ["{Cliente}", "{Barbero}", "{Servicio}", "{Fecha}", "{Hora}", "{Barberia}"];
}
