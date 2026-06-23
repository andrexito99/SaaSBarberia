using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Models;
using SaaSBarberia.Services;
using System.Security.Claims;
using System.Threading.Tasks;

[Authorize(Roles = "SuperAdmin,AdminBarberia")] // 🔒 Protegido para administradores
public class UsuariosController : Controller
{
    private readonly UsuarioService _usuarioService;

    public UsuariosController(UsuarioService usuarioService)
    {
        _usuarioService = usuarioService;
    }

    private int CurrentUsuarioId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId) ? userId : 0;
    private string CurrentUserIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

    // GET: Usuarios
    public async Task<IActionResult> Index()
    {
        return View(await _usuarioService.ObtenerTodosConRelacionesAsync());
    }

    // GET: Usuarios/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var usuario = await _usuarioService.ObtenerPorIdAsync(id.Value);
        if (usuario == null) return NotFound();

        return View(usuario);
    }

    // GET: Usuarios/Create
    public async Task<IActionResult> Create()
    {
        ViewBag.RoleId = new SelectList(await _usuarioService.ObtenerRolesAsync(), "Id", "NombreRole");
        ViewBag.TenantId = new SelectList(await _usuarioService.ObtenerTenantsAsync(), "Id", "NombreNegocio");

        var nuevoUsuario = new Usuario { Activo = true };
        return View(nuevoUsuario);
    }

    // POST: Usuarios/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,TenantId,RoleId,Nombre,Email,PasswordHash,Telefono,Activo")] Usuario usuario)
    {
        ModelState.Remove(nameof(usuario.Role));
        ModelState.Remove(nameof(usuario.Tenant));
        ModelState.Remove(nameof(usuario.Barberos));
        ModelState.Remove(nameof(usuario.Cita));

        if (ModelState.IsValid)
        {
            bool elEmailYaExiste = await _usuarioService.ExisteEmailAsync(usuario.Email);
            bool elTelefonoYaExiste = !string.IsNullOrEmpty(usuario.Telefono) && await _usuarioService.ExisteTelefonoAsync(usuario.Telefono);

            if (elEmailYaExiste) ModelState.AddModelError("Email", "Este correo electrónico ya se encuentra registrado, care monda.");
            if (elTelefonoYaExiste) ModelState.AddModelError("Telefono", "Este número de teléfono ya está asignado a otro usuario.");

            if (elEmailYaExiste || elTelefonoYaExiste)
            {
                ViewBag.RoleId = new SelectList(await _usuarioService.ObtenerRolesAsync(), "Id", "NombreRole", usuario.RoleId);
                ViewBag.TenantId = new SelectList(await _usuarioService.ObtenerTenantsAsync(), "Id", "NombreNegocio", usuario.TenantId);
                return View(usuario);
            }

            // Pasamos quién está creando esta cuenta nueva
            await _usuarioService.GuardarAsync(usuario, CurrentUsuarioId, CurrentUserIp);
            return RedirectToAction(nameof(Index));
        }

        ViewBag.RoleId = new SelectList(await _usuarioService.ObtenerRolesAsync(), "Id", "NombreRole", usuario.RoleId);
        ViewBag.TenantId = new SelectList(await _usuarioService.ObtenerTenantsAsync(), "Id", "NombreNegocio", usuario.TenantId);
        return View(usuario);
    }

    // GET: Usuarios/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var usuario = await _usuarioService.ObtenerPorIdAsync(id.Value);
        if (usuario == null) return NotFound();

        ViewBag.RoleId = new SelectList(await _usuarioService.ObtenerRolesAsync(), "Id", "NombreRole", usuario.RoleId);
        ViewBag.TenantId = new SelectList(await _usuarioService.ObtenerTenantsAsync(), "Id", "NombreNegocio", usuario.TenantId);
        return View(usuario);
    }

    // POST: Usuarios/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,TenantId,RoleId,Nombre,Email,PasswordHash,Telefono,Activo")] Usuario usuario)
    {
        if (id != usuario.Id) return NotFound();

        ModelState.Remove(nameof(usuario.Role));
        ModelState.Remove(nameof(usuario.Tenant));
        ModelState.Remove(nameof(usuario.Barberos));
        ModelState.Remove(nameof(usuario.Cita));

        if (ModelState.IsValid)
        {
            bool elEmailYaExiste = await _usuarioService.ExisteEmailParaOtroUsuarioAsync(usuario.Email, usuario.Id);
            bool elTelefonoYaExiste = !string.IsNullOrEmpty(usuario.Telefono) && await _usuarioService.ExisteTelefonoParaOtroUsuarioAsync(usuario.Telefono, usuario.Id);

            if (elEmailYaExiste) ModelState.AddModelError("Email", "No puedes usar este correo, ya está asignado a otra cuenta.");
            if (elTelefonoYaExiste) ModelState.AddModelError("Telefono", "No puedes usar este teléfono, ya está asignado a otra cuenta.");

            if (elEmailYaExiste || elTelefonoYaExiste)
            {
                ViewBag.RoleId = new SelectList(await _usuarioService.ObtenerRolesAsync(), "Id", "NombreRole", usuario.RoleId);
                ViewBag.TenantId = new SelectList(await _usuarioService.ObtenerTenantsAsync(), "Id", "NombreNegocio", usuario.TenantId);
                return View(usuario);
            }

            try
            {
                await _usuarioService.GuardarAsync(usuario, CurrentUsuarioId, CurrentUserIp);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_usuarioService.Existe(usuario.Id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }
        ViewBag.RoleId = new SelectList(await _usuarioService.ObtenerRolesAsync(), "Id", "NombreRole", usuario.RoleId);
        ViewBag.TenantId = new SelectList(await _usuarioService.ObtenerTenantsAsync(), "Id", "NombreNegocio", usuario.TenantId);
        return View(usuario);
    }

    // GET: Usuarios/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var usuario = await _usuarioService.ObtenerPorIdAsync(id.Value);
        if (usuario == null) return NotFound();

        return View(usuario);
    }

    // POST: Usuarios/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        await _usuarioService.EliminarAsync(id, CurrentUsuarioId, CurrentUserIp);
        return RedirectToAction(nameof(Index));
    }
}