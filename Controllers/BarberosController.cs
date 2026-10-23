using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Models;
using SaaSBarberia.Services;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

[Authorize(Roles = "Administrador,SuperAdmin")]
public class BarberosController : Controller
{
    private readonly BarberoService _barberoService;
    private readonly CitaService _citaService;
    private readonly SupabaseStorageService _storageService;

    public BarberosController(BarberoService barberoService, CitaService citaService, SupabaseStorageService storageService)
    {
        _barberoService = barberoService;
        _citaService    = citaService;
        _storageService = storageService;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────

    private int    CurrentUsuarioId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int uid) ? uid : 0;
    private string CurrentUserIp    => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

    // ── Index ─────────────────────────────────────────────────────────────────────────

    public async Task<IActionResult> Index()
    {
        if (CurrentUsuarioId == 0) return RedirectToAction("Login", "Account");

        var barberos = await _barberoService.ObtenerTodosConRelacionesAsync();

        // Filtro Multi-Tenant: Administrador y Barbero solo ven los de su sede
        if (User.IsInRole("Administrador") || User.IsInRole("Barbero") || User.IsInRole("Cliente"))
        {
            var cuenta = await _citaService.ObtenerClientePorIdAsync(CurrentUsuarioId);
            if (cuenta?.TenantId != null)
                barberos = barberos.Where(b => b.TenantId == cuenta.TenantId.Value).ToList();
        }
        return View(barberos);
    }

    // ── Details ───────────────────────────────────────────────────────────────────────

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var barbero = await _barberoService.ObtenerPorIdAsync(id.Value);
        if (barbero == null) return NotFound();
        return View(barbero);
    }

    // ── Create GET ────────────────────────────────────────────────────────────────────

    public async Task<IActionResult> Create()
    {
        if (CurrentUsuarioId == 0) return RedirectToAction("Login", "Account");

        var usuarios = await _barberoService.ObtenerUsuariosDisponiblesAsync();
        var tenants  = await _barberoService.ObtenerTenantsAsync();

        if (User.IsInRole("Administrador"))
        {
            var adminDb = await _citaService.ObtenerClientePorIdAsync(CurrentUsuarioId);
            if (adminDb?.TenantId != null)
            {
                int miTenantId = adminDb.TenantId.Value;
                ViewBag.UsuarioId = new SelectList(usuarios.Where(u => u.TenantId == miTenantId), "Id", "Nombre");
                ViewBag.TenantId  = new SelectList(tenants.Where(t => t.Id == miTenantId), "Id", "NombreNegocio", miTenantId);
            }
        }
        else
        {
            ViewBag.UsuarioId = new SelectList(usuarios, "Id", "Nombre");
            ViewBag.TenantId  = new SelectList(tenants, "Id", "NombreNegocio");
        }
        return View(new Barbero());
    }

    // ── Create POST ───────────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("Id,TenantId,UsuarioId,Especialidad,ComisionPorcentaje,RutaFoto")] Barbero barbero,
        IFormFile? fotoArchivo)   // ← name debe coincidir con el input[name="fotoArchivo"]
    {
        Console.WriteLine($"---> [Barberos/Create POST] Archivo: " +
                          $"{(fotoArchivo == null ? "NO LLEGÓ" : $"{fotoArchivo.FileName} ({fotoArchivo.Length} bytes)")}");

        ModelState.Remove(nameof(barbero.Tenant));
        ModelState.Remove(nameof(barbero.Usuario));
        ModelState.Remove(nameof(barbero.RutaFoto));

        if (!ModelState.IsValid)
        {
            Console.WriteLine("---> [Barberos/Create POST] ModelState INVÁLIDO:");
            foreach (var kv in ModelState)
                foreach (var err in kv.Value.Errors)
                    Console.WriteLine($"     Campo '{kv.Key}': {err.ErrorMessage}");

            var usuarios = await _barberoService.ObtenerUsuariosDisponiblesAsync();
            var tenants  = await _barberoService.ObtenerTenantsAsync();
            ViewBag.UsuarioId = new SelectList(usuarios, "Id", "Nombre", barbero.UsuarioId);
            ViewBag.TenantId  = new SelectList(tenants,  "Id", "NombreNegocio", barbero.TenantId);
            return View(barbero);
        }

        // Subir foto a Supabase con aislamiento por Tenant
        if (fotoArchivo != null && fotoArchivo.Length > 0)
        {
            var url = await _storageService.SubirImagenAsync(
                fotoArchivo,
                $"barberos/tenant_{barbero.TenantId}");

            if (!string.IsNullOrEmpty(url))
            {
                barbero.RutaFoto = url;
                Console.WriteLine($"---> [Barberos/Create POST] RutaFoto asignada: {url}");
            }
        }

        await _barberoService.GuardarAsync(barbero, CurrentUsuarioId, CurrentUserIp);
        return RedirectToAction(nameof(Index));
    }

    // ── Edit GET ──────────────────────────────────────────────────────────────────────

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var barbero = await _barberoService.ObtenerPorIdAsync(id.Value);
        if (barbero == null) return NotFound();

        var usuarios = await _barberoService.ObtenerUsuariosDisponiblesAsync();
        var tenants  = await _barberoService.ObtenerTenantsAsync();
        ViewBag.UsuarioId = new SelectList(usuarios, "Id", "Nombre", barbero.UsuarioId);
        ViewBag.TenantId  = new SelectList(tenants,  "Id", "NombreNegocio", barbero.TenantId);
        return View(barbero);
    }

    // ── Edit POST ─────────────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("Id,TenantId,UsuarioId,Especialidad,ComisionPorcentaje,RutaFoto")] Barbero barbero,
        IFormFile? fotoInput)   // ← name debe coincidir con el input[name="fotoInput"]
    {
        if (id != barbero.Id) return NotFound();

        ModelState.Remove(nameof(barbero.Tenant));
        ModelState.Remove(nameof(barbero.Usuario));
        ModelState.Remove(nameof(barbero.RutaFoto));

        if (!ModelState.IsValid) return View(barbero);

        try
        {
            if (fotoInput != null && fotoInput.Length > 0)
            {
                // Subir nueva foto y reemplazar URL
                var url = await _storageService.SubirImagenAsync(
                    fotoInput,
                    $"barberos/tenant_{barbero.TenantId}");

                if (!string.IsNullOrEmpty(url))
                {
                    barbero.RutaFoto = url;
                    Console.WriteLine($"---> [Barberos/Edit POST] RutaFoto actualizada: {url}");
                }
            }
            // Si fotoInput es null, la vista envía la foto existente
            // vía <input type="hidden" asp-for="RutaFoto">, BarberoService la respeta.

            await _barberoService.GuardarAsync(barbero, CurrentUsuarioId, CurrentUserIp);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_barberoService.Existe(barbero.Id)) return NotFound();
            throw;
        }

        return RedirectToAction(nameof(Index));
    }

    // ── Delete ────────────────────────────────────────────────────────────────────────

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var barbero = await _barberoService.ObtenerPorIdAsync(id.Value);
        if (barbero == null) return NotFound();
        return View(barbero);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _barberoService.EliminarAsync(id, CurrentUsuarioId, CurrentUserIp);
        return RedirectToAction(nameof(Index));
    }
}