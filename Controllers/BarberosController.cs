using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Models;
using SaaSBarberia.Services;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting; // 🔥 IMPORTANTE
using Microsoft.AspNetCore.Authorization;

[Authorize(Roles = "Administrador,SuperAdmin")]
public class BarberosController : Controller
{
    private readonly BarberoService _barberoService;
    private readonly CitaService _citaService;
    private readonly IWebHostEnvironment _webHostEnvironment; // 🔥 Manejo de archivos en disco

    public BarberosController(BarberoService barberoService, CitaService citaService, IWebHostEnvironment webHostEnvironment)
    {
        _barberoService = barberoService;
        _citaService = citaService;
        _webHostEnvironment = webHostEnvironment;
    }

    private int CurrentUsuarioId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId) ? userId : 0;
    private string CurrentUserIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

    public async Task<IActionResult> Index()
    {
        if (CurrentUsuarioId == 0) return RedirectToAction("Login", "Account");
        var barberos = await _barberoService.ObtenerTodosConRelacionesAsync();

        if (User.IsInRole("Administador") || User.IsInRole("Barbero") || User.IsInRole("Cliente"))
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
    // 🔥 CORREGIDO: Recibe 'fotoArchivo' mapeado con la vista y bindea 'FotoRuta' (string)
    public async Task<IActionResult> Create([Bind("Id,TenantId,UsuarioId,Especialidad,ComisionPorcentaje,RutaFoto")] Barbero barbero, IFormFile? fotoArchivo)
    {
        ModelState.Remove(nameof(barbero.Tenant));
        ModelState.Remove(nameof(barbero.Usuario));

        if (ModelState.IsValid)
        {
            // 💾 GUARDADO EN DISCO
            if (fotoArchivo != null && fotoArchivo.Length > 0)
            {
                string carpetaSubidas = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "barberos");
                if (!Directory.Exists(carpetaSubidas)) Directory.CreateDirectory(carpetaSubidas);

                string nombreUnico = Guid.NewGuid().ToString() + Path.GetExtension(fotoArchivo.FileName);
                string rutaCompleta = Path.Combine(carpetaSubidas, nombreUnico);

                using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                {
                    await fotoArchivo.CopyToAsync(stream);
                }
                barbero.RutaFoto = "/uploads/barberos/" + nombreUnico;
            }

            await _barberoService.GuardarAsync(barbero, CurrentUsuarioId, CurrentUserIp);
            return RedirectToAction(nameof(Index));
        }

        var usuarios = await _barberoService.ObtenerUsuariosDisponiblesAsync();
        var tenants = await _barberoService.ObtenerTenantsAsync();
        // (Mantenemos recarga de combos por error de validación...)
        return View(barbero);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var barbero = await _barberoService.ObtenerPorIdAsync(id.Value);
        if (barbero == null) return NotFound();

        var usuarios = await _barberoService.ObtenerUsuariosDisponiblesAsync();
        var tenants = await _barberoService.ObtenerTenantsAsync();
        // Combos...
        return View(barbero);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // 🔥 CORREGIDO 1: Cambiado 'FotoRuta' por 'RutaFoto' en el Bind para que reciba el hidden correctamente
    // 🔥 CORREGIDO 2: Parámetro cambiado de 'fotoArchivo' a 'fotoInput' para que haga match perfecto con el name de tu vista
    public async Task<IActionResult> Edit(int id, [Bind("Id,TenantId,UsuarioId,Especialidad,ComisionPorcentaje,RutaFoto")] Barbero barbero, IFormFile? fotoInput)
    {
        if (id != barbero.Id) return NotFound();

        ModelState.Remove(nameof(barbero.Tenant));
        ModelState.Remove(nameof(barbero.Usuario));

        if (ModelState.IsValid)
        {
            try
            {
                // 📸 1. Si el usuario de verdad subió un archivo nuevo por el formulario
                if (fotoInput != null && fotoInput.Length > 0)
                {
                    // 🗑️ Borrar foto vieja si existe en el disco duro para no acumular basura
                    var barberoExistente = await _barberoService.ObtenerPorIdAsync(id);
                    if (barberoExistente != null && !string.IsNullOrEmpty(barberoExistente.RutaFoto))
                    {
                        string rutaVieja = Path.Combine(_webHostEnvironment.WebRootPath, barberoExistente.RutaFoto.TrimStart('/'));
                        if (System.IO.File.Exists(rutaVieja)) System.IO.File.Delete(rutaVieja);
                    }

                    // 💾 Guardar foto nueva en la carpeta uploads
                    string carpetaSubidas = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "barberos");
                    if (!Directory.Exists(carpetaSubidas)) Directory.CreateDirectory(carpetaSubidas);

                    string nombreUnico = Guid.NewGuid().ToString() + Path.GetExtension(fotoInput.FileName);
                    string rutaCompleta = Path.Combine(carpetaSubidas, nombreUnico);

                    using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                    {
                        await fotoInput.CopyToAsync(stream);
                    }

                    // Clavamos la nueva ruta generada
                    barbero.RutaFoto = "/uploads/barberos/" + nombreUnico;
                }
                else
                {
                    // 🔄 2. Si no subió foto nueva, mantenemos la ruta que ya traía el input hidden de la vista
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
        // 🗑️ Limpieza física de la foto antes de borrar de la BD
        var barbero = await _barberoService.ObtenerPorIdAsync(id);
        if (barbero != null && !string.IsNullOrEmpty(barbero.RutaFoto))
        {
            string rutaArchivo = Path.Combine(_webHostEnvironment.WebRootPath, barbero.RutaFoto.TrimStart('/'));
            if (System.IO.File.Exists(rutaArchivo)) System.IO.File.Delete(rutaArchivo);
        }

        await _barberoService.EliminarAsync(id, CurrentUsuarioId, CurrentUserIp);
        return RedirectToAction(nameof(Index));
    }
}