using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Models;
using SaaSBarberia.Services;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

[Authorize]
public class BarberosController : Controller
{
    private readonly BarberoService _barberoService;
    private readonly CitaService _citaService;

    public BarberosController(BarberoService barberoService, CitaService citaService)
    {
        _barberoService = barberoService;
        _citaService = citaService;
    }

    // 🔍 PROPIEDADES DINÁMICAS PARA LOGS Y AUDITORÍA
    private int CurrentUsuarioId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId) ? userId : 0;
    private string CurrentUserIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

    // GET: Barberoes
    public async Task<IActionResult> Index()
    {
        if (CurrentUsuarioId == 0) return RedirectToAction("Login", "Account");

        var barberos = await _barberoService.ObtenerTodosConRelacionesAsync();

        if (User.IsInRole("AdminBarberia") || User.IsInRole("Barbero") || User.IsInRole("Cliente"))
        {
            var cuenta = await _citaService.ObtenerClientePorIdAsync(CurrentUsuarioId);
            if (cuenta != null && cuenta.TenantId != null)
            {
                barberos = barberos.Where(b => b.TenantId == cuenta.TenantId.Value).ToList();
            }
        }

        return View(barberos);
    }

    // GET: Barberoes/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var barbero = await _barberoService.ObtenerPorIdAsync(id.Value);
        if (barbero == null) return NotFound();

        return View(barbero);
    }

    // GET: Barberoes/Create
    public async Task<IActionResult> Create()
    {
        if (CurrentUsuarioId == 0) return RedirectToAction("Login", "Account");

        var usuarios = await _barberoService.ObtenerUsuariosDisponiblesAsync();
        var tenants = await _barberoService.ObtenerTenantsAsync();

        if (User.IsInRole("AdminBarberia"))
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

    // POST: Barberoes/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,TenantId,UsuarioId,Especialidad,ComisionPorcentaje")] Barbero barbero, IFormFile? fotoInput)
    {
        ModelState.Remove(nameof(barbero.Tenant));
        ModelState.Remove(nameof(barbero.Usuario));
        ModelState.Remove(nameof(barbero.Foto));

        if (ModelState.IsValid)
        {
            if (fotoInput != null && fotoInput.Length > 0)
            {
                using (var memoryStream = new MemoryStream())
                {
                    await fotoInput.CopyToAsync(memoryStream);
                    barbero.Foto = memoryStream.ToArray();
                }
            }

            await _barberoService.GuardarAsync(barbero, CurrentUsuarioId, CurrentUserIp);
            return RedirectToAction(nameof(Index));
        }

        var usuarios = await _barberoService.ObtenerUsuariosDisponiblesAsync();
        var tenants = await _barberoService.ObtenerTenantsAsync();

        if (User.IsInRole("AdminBarberia"))
        {
            ViewBag.UsuarioId = new SelectList(usuarios.Where(u => u.TenantId == barbero.TenantId), "Id", "Nombre", barbero.UsuarioId);
            ViewBag.TenantId = new SelectList(tenants.Where(t => t.Id == barbero.TenantId), "Id", "NombreNegocio", barbero.TenantId);
        }
        else
        {
            ViewBag.UsuarioId = new SelectList(usuarios, "Id", "Nombre", barbero.UsuarioId);
            ViewBag.TenantId = new SelectList(tenants, "Id", "NombreNegocio", barbero.TenantId);
        }
        return View(barbero);
    }

    // GET: Barberoes/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var barbero = await _barberoService.ObtenerPorIdAsync(id.Value);
        if (barbero == null) return NotFound();

        var usuarios = await _barberoService.ObtenerUsuariosDisponiblesAsync();
        var tenants = await _barberoService.ObtenerTenantsAsync();

        if (User.IsInRole("AdminBarberia"))
        {
            ViewBag.UsuarioId = new SelectList(usuarios.Where(u => u.TenantId == barbero.TenantId), "Id", "Nombre", barbero.UsuarioId);
            ViewBag.TenantId = new SelectList(tenants.Where(t => t.Id == barbero.TenantId), "Id", "NombreNegocio", barbero.TenantId);
        }
        else
        {
            ViewBag.UsuarioId = new SelectList(usuarios, "Id", "Nombre", barbero.UsuarioId);
            ViewBag.TenantId = new SelectList(tenants, "Id", "NombreNegocio", barbero.TenantId);
        }
        return View(barbero);
    }

    // POST: Barberoes/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,TenantId,UsuarioId,Especialidad,ComisionPorcentaje")] Barbero barbero, IFormFile? fotoInput)
    {
        if (id != barbero.Id) return NotFound();

        ModelState.Remove(nameof(barbero.Tenant));
        ModelState.Remove(nameof(barbero.Usuario));
        ModelState.Remove(nameof(barbero.Foto));

        if (ModelState.IsValid)
        {
            try
            {
                if (fotoInput != null && fotoInput.Length > 0)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await fotoInput.CopyToAsync(memoryStream);
                        barbero.Foto = memoryStream.ToArray();
                    }
                }
                else
                {
                    var barberoExistente = await _barberoService.ObtenerPorIdAsync(id);
                    if (barberoExistente != null)
                    {
                        barbero.Foto = barberoExistente.Foto;
                    }
                }

                await _barberoService.GuardarAsync(barbero, CurrentUsuarioId, CurrentUserIp);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_barberoService.Existe(barbero.Id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }

        var usuarios = await _barberoService.ObtenerUsuariosDisponiblesAsync();
        var tenants = await _barberoService.ObtenerTenantsAsync();

        if (User.IsInRole("AdminBarberia"))
        {
            ViewBag.UsuarioId = new SelectList(usuarios.Where(u => u.TenantId == barbero.TenantId), "Id", "Nombre", barbero.UsuarioId);
            ViewBag.TenantId = new SelectList(tenants.Where(t => t.Id == barbero.TenantId), "Id", "NombreNegocio", barbero.TenantId);
        }
        else
        {
            ViewBag.UsuarioId = new SelectList(usuarios, "Id", "Nombre", barbero.UsuarioId);
            ViewBag.TenantId = new SelectList(tenants, "Id", "NombreNegocio", barbero.TenantId);
        }
        return View(barbero);
    }

    // GET: Barberoes/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var barbero = await _barberoService.ObtenerPorIdAsync(id.Value);
        if (barbero == null) return NotFound();

        return View(barbero);
    }

    // POST: Barberoes/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _barberoService.EliminarAsync(id, CurrentUsuarioId, CurrentUserIp);
        return RedirectToAction(nameof(Index));
    }
}