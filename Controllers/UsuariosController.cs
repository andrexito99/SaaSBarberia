using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Models;
using SaaSBarberia.Services;
using System.Security.Claims;

[Authorize(Roles = "SuperAdmin,Administrador")] 
public class UsuariosController : Controller
{
    private readonly UsuarioService _usuarioService;

    public UsuariosController(UsuarioService usuarioService) => _usuarioService = usuarioService;

    private int CurrentUsuarioId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId) ? userId : 0;
    private int? CurrentTenantId => int.TryParse(User.FindFirst("TenantId")?.Value, out int tenantId) ? tenantId : null;
    private bool EsSuperAdmin => User.IsInRole("SuperAdmin");
    private string CurrentUserIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

    private bool TryGetTenantAdministrado(out int tenantId)
    {
        tenantId = CurrentTenantId ?? 0;
        return EsSuperAdmin || tenantId > 0;
    }

    private async Task CargarListasAsync(int? roleId = null, int? tenantId = null)
    {
        var tenantSeleccionado = EsSuperAdmin ? tenantId : CurrentTenantId;
        ViewBag.RoleId = new SelectList(await _usuarioService.ObtenerRolesAsync(EsSuperAdmin), "Id", "NombreRole", roleId);
        ViewBag.TenantId = new SelectList(
            await _usuarioService.ObtenerTenantsAsync(CurrentTenantId, EsSuperAdmin),
            "Id", "NombreNegocio", tenantSeleccionado);
    }

    private async Task<bool> RolPermitidoAsync(int roleId) =>
        EsSuperAdmin || !await _usuarioService.EsRolSuperAdminAsync(roleId);

    public async Task<IActionResult> Index()
    {
        if (!TryGetTenantAdministrado(out _)) return Forbid();
        return View(await _usuarioService.ObtenerTodosConRelacionesAsync(CurrentTenantId, EsSuperAdmin));
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null || !TryGetTenantAdministrado(out _)) return NotFound();

        var usuario = await _usuarioService.ObtenerPorIdAsync(id.Value, CurrentTenantId, EsSuperAdmin);
        return usuario == null ? NotFound() : View(usuario);
    }

    public async Task<IActionResult> Create()
    {
        if (!TryGetTenantAdministrado(out _)) return Forbid();

        var nuevoUsuario = new Usuario { Activo = true, TenantId = EsSuperAdmin ? null : CurrentTenantId };
        await CargarListasAsync(tenantId: nuevoUsuario.TenantId);
        return View(nuevoUsuario);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("RoleId,Nombre,Email,PasswordHash,Telefono,Activo")] Usuario usuario, int? tenantId)
    {
        if (!TryGetTenantAdministrado(out _)) return Forbid();

        // TenantId is never taken from the bound model. Only SuperAdmin can select it.
        usuario.TenantId = EsSuperAdmin ? tenantId : CurrentTenantId;
        ModelState.Remove(nameof(usuario.Role));
        ModelState.Remove(nameof(usuario.Tenant));
        ModelState.Remove(nameof(usuario.Barberos));
        ModelState.Remove(nameof(usuario.Cita));
        if (!await RolPermitidoAsync(usuario.RoleId))
            ModelState.AddModelError(nameof(usuario.RoleId), "No tienes permiso para asignar el rol SuperAdmin.");

        if (ModelState.IsValid)
        {
            bool emailExiste = await _usuarioService.ExisteEmailAsync(usuario.Email);
            bool telefonoExiste = !string.IsNullOrEmpty(usuario.Telefono) && await _usuarioService.ExisteTelefonoAsync(usuario.Telefono);
            if (emailExiste) ModelState.AddModelError("Email", "Este correo electrónico ya se encuentra registrado.");
            if (telefonoExiste) ModelState.AddModelError("Telefono", "Este número de teléfono ya está asignado a otro usuario.");

            if (!emailExiste && !telefonoExiste)
            {
                try
                {
                    await _usuarioService.CrearAsync(usuario, CurrentUsuarioId, CurrentUserIp, CurrentTenantId, EsSuperAdmin);
                    return RedirectToAction(nameof(Index));
                }
                catch (UnauthorizedAccessException)
                {
                    ModelState.AddModelError(string.Empty, "No tienes permiso para crear este usuario.");
                }
            }
        }

        await CargarListasAsync(usuario.RoleId, usuario.TenantId);
        return View(usuario);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null || !TryGetTenantAdministrado(out _)) return NotFound();

        var usuario = await _usuarioService.ObtenerPorIdAsync(id.Value, CurrentTenantId, EsSuperAdmin);
        if (usuario == null) return NotFound();

        await CargarListasAsync(usuario.RoleId, usuario.TenantId);
        return View(usuario);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("RoleId,Nombre,Email,PasswordHash,Telefono,Activo")] Usuario usuario, int? tenantId)
    {
        if (!TryGetTenantAdministrado(out _)) return Forbid();

        var existente = await _usuarioService.ObtenerPorIdAsync(id, CurrentTenantId, EsSuperAdmin);
        if (existente == null) return NotFound();

        usuario.Id = id;
        // A tenant administrator always keeps the authorized entity's tenant.
        usuario.TenantId = EsSuperAdmin ? tenantId : existente.TenantId;
        ModelState.Remove(nameof(usuario.Role));
        ModelState.Remove(nameof(usuario.Tenant));
        ModelState.Remove(nameof(usuario.Barberos));
        ModelState.Remove(nameof(usuario.Cita));
        if (!await RolPermitidoAsync(usuario.RoleId))
            ModelState.AddModelError(nameof(usuario.RoleId), "No tienes permiso para asignar el rol SuperAdmin.");
        if (!EsSuperAdmin && id == CurrentUsuarioId && usuario.RoleId != existente.RoleId)
            ModelState.AddModelError(nameof(usuario.RoleId), "No puedes modificar el rol de tu propia cuenta.");

        if (ModelState.IsValid)
        {
            bool emailExiste = await _usuarioService.ExisteEmailParaOtroUsuarioAsync(usuario.Email, id);
            bool telefonoExiste = !string.IsNullOrEmpty(usuario.Telefono) && await _usuarioService.ExisteTelefonoParaOtroUsuarioAsync(usuario.Telefono, id);
            if (emailExiste) ModelState.AddModelError("Email", "No puedes usar este correo, ya está asignado a otra cuenta.");
            if (telefonoExiste) ModelState.AddModelError("Telefono", "No puedes usar este teléfono, ya está asignado a otra cuenta.");

            if (!emailExiste && !telefonoExiste)
            {
                try
                {
                    if (!await _usuarioService.ActualizarAsync(usuario, CurrentUsuarioId, CurrentUserIp, CurrentTenantId, EsSuperAdmin))
                        return NotFound();
                    return RedirectToAction(nameof(Index));
                }
                catch (UnauthorizedAccessException)
                {
                    ModelState.AddModelError(string.Empty, "No tienes permiso para modificar este usuario.");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_usuarioService.Existe(id)) return NotFound();
                    throw;
                }
            }
        }

        await CargarListasAsync(usuario.RoleId, usuario.TenantId);
        return View(usuario);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null || !TryGetTenantAdministrado(out _)) return NotFound();

        var usuario = await _usuarioService.ObtenerPorIdAsync(id.Value, CurrentTenantId, EsSuperAdmin);
        return usuario == null ? NotFound() : View(usuario);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (!TryGetTenantAdministrado(out _)) return Forbid();
        if (!await _usuarioService.EliminarAsync(id, CurrentUsuarioId, CurrentUserIp, CurrentTenantId, EsSuperAdmin))
            return NotFound();
        return RedirectToAction(nameof(Index));
    }
}
