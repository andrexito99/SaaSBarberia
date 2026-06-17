using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Models;
using SaaSBarberia.Services;
using System.Threading.Tasks;

public class UsuariosController : Controller
{
    private readonly UsuarioService _usuarioService;

    public UsuariosController(UsuarioService usuarioService)
    {
        _usuarioService = usuarioService;
    }

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
        // 🌟 REMOCIÓN ESTRATÉGICA: Ignoramos las entidades virtuales para evitar el rebote del formulario
        ModelState.Remove(nameof(usuario.Role));
        ModelState.Remove(nameof(usuario.Tenant));
        ModelState.Remove(nameof(usuario.Barberos));
        ModelState.Remove(nameof(usuario.Cita));

        if (ModelState.IsValid)
        {
            await _usuarioService.GuardarAsync(usuario);
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

        // 🌟 REMOCIÓN ESTRATÉGICA: También limpiamos el Edit para actualizaciones exitosas
        ModelState.Remove(nameof(usuario.Role));
        ModelState.Remove(nameof(usuario.Tenant));
        ModelState.Remove(nameof(usuario.Barberos));
        ModelState.Remove(nameof(usuario.Cita));

        if (ModelState.IsValid)
        {
            try
            {
                await _usuarioService.GuardarAsync(usuario);
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
        await _usuarioService.EliminarAsync(id);
        return RedirectToAction(nameof(Index));
    }
}