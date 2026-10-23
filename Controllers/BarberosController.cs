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
    private readonly SupabaseStorageService _storageService; // 🚀 Reemplazado por Supabase Storage

    public BarberosController(BarberoService barberoService, CitaService citaService, SupabaseStorageService storageService)
    {
        _barberoService = barberoService;
        _citaService = citaService;
        _storageService = storageService;
    }

    private int CurrentUsuarioId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId) ? userId : 0;
    private string CurrentUserIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

    public async Task<IActionResult> Index()
    {
        if (CurrentUsuarioId == 0) return RedirectToAction("Login", "Account");
        var barberos = await _barberoService.ObtenerTodosConRelacionesAsync();

        if (User.IsInRole("Administrador") || User.IsInRole("Barbero") || User.IsInRole("Cliente"))
        {
            var cuenta = await _citaService.ObtenerClientePorIdAsync(CurrentUsuarioId);
            if (cuenta != null && cuenta.TenantId != null)
            {
                barberos = barberos.Where(b => b.TenantId == cuenta.TenantId.Value).ToList();
            }
        }
        return View(barberos);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var barbero = await _barberoService.ObtenerPorIdAsync(id.Value);
        if (barbero == null) return NotFound();
        return View(barbero);
    }

    public async Task<IActionResult> Create()
    {
        if (CurrentUsuarioId == 0) return RedirectToAction("Login", "Account");
        var usuarios = await _barberoService.ObtenerUsuariosDisponiblesAsync();
        var tenants = await _barberoService.ObtenerTenantsAsync();

        if (User.IsInRole("Administrador"))
        {
            var adminDb = await _citaService.ObtenerClientePorIdAsync(CurrentUsuarioId);
            if (adminDb != null && adminDb.TenantId != null)
            {
                int miTenantId = adminDb.TenantId.Value;
                ViewBag.UsuarioId = new SelectList(usuarios.Where(u => u.TenantId == miTenantId), "Id", "Nombre");
                ViewBag.TenantId = new SelectList(tenants.Where(t => t.Id == miTenantId), "Id", "NombreNegocio", miTenantId);
            }
        }
        else
        {
            ViewBag.UsuarioId = new SelectList(usuarios, "Id", "Nombre");
            ViewBag.TenantId = new SelectList(tenants, "Id", "NombreNegocio");
        }
        return View(new Barbero());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,TenantId,UsuarioId,Especialidad,ComisionPorcentaje,RutaFoto")] Barbero barbero, IFormFile? fotoArchivo)
    {
        ModelState.Remove(nameof(barbero.Tenant));
        ModelState.Remove(nameof(barbero.Usuario));

        if (ModelState.IsValid)
        {
            // ☁️ GUARDADO EN SUPABASE STORAGE
            if (fotoArchivo != null && fotoArchivo.Length > 0)
            {
                string? urlImagen = await _storageService.SubirImagenAsync(fotoArchivo, $"barberos/tenant_{barbero.TenantId}");
                if (!string.IsNullOrEmpty(urlImagen))
                {
                    barbero.RutaFoto = urlImagen;
                }
            }

            await _barberoService.GuardarAsync(barbero, CurrentUsuarioId, CurrentUserIp);
            return RedirectToAction(nameof(Index));
        }

        var usuarios = await _barberoService.ObtenerUsuariosDisponiblesAsync();
        var tenants = await _barberoService.ObtenerTenantsAsync();
        return View(barbero);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var barbero = await _barberoService.ObtenerPorIdAsync(id.Value);
        if (barbero == null) return NotFound();

        var usuarios = await _barberoService.ObtenerUsuariosDisponiblesAsync();
        var tenants = await _barberoService.ObtenerTenantsAsync();
        return View(barbero);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,TenantId,UsuarioId,Especialidad,ComisionPorcentaje,RutaFoto")] Barbero barbero, IFormFile? fotoInput)
    {
        if (id != barbero.Id) return NotFound();

        ModelState.Remove(nameof(barbero.Tenant));
        ModelState.Remove(nameof(barbero.Usuario));

        if (ModelState.IsValid)
        {
            try
            {
                // 📸 1. Si subió una foto nueva, la mandamos a Supabase Storage
                if (fotoInput != null && fotoInput.Length > 0)
                {
                    string? urlImagen = await _storageService.SubirImagenAsync(fotoInput, $"barberos/tenant_{barbero.TenantId}");
                    if (!string.IsNullOrEmpty(urlImagen))
                    {
                        barbero.RutaFoto = urlImagen;
                    }
                }
                else
                {
                    // 🔄 2. Si no subió foto nueva, mantenemos la ruta que ya traía el input hidden
                    var barberoExistente = await _barberoService.ObtenerPorIdAsync(id);
                    if (barberoExistente != null) barbero.RutaFoto = barberoExistente.RutaFoto;
                }

                // Guardamos con auditoría incorporada
                await _barberoService.GuardarAsync(barbero, CurrentUsuarioId, CurrentUserIp);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_barberoService.Existe(barbero.Id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(barbero);
    }

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
        // En Supabase Storage eliminamos el registro y la referencia de la BD; el archivo queda en el bucket o se gestiona online.
        await _barberoService.EliminarAsync(id, CurrentUsuarioId, CurrentUserIp);
        return RedirectToAction(nameof(Index));
    }
}