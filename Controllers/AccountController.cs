using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;
using SaaSBarberia.Services;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace SaaSBarberia.Controllers
{
    public class AccountController : Controller
    {
        private readonly BarberSaaSDbContext _context;
        private readonly IAuditoriaService _auditoriaService;

        public AccountController(BarberSaaSDbContext context, IAuditoriaService auditoriaService, IPasswordHasher<Usuario> passwordHasher)
        {
            _context = context;
            _auditoriaService = auditoriaService;
            _passwordHasher = passwordHasher;
        }

        private readonly IPasswordHasher<Usuario> _passwordHasher;

        private string CurrentUserIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "El correo y la contraseña son obligatorios, care monda.");
                return View();
            }

            var usuario = await _context.Usuarios
                .IgnoreQueryFilters()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == email);

            // ⚠️ CONTROL DE FALLOS: Se queda activo para registrar intrusos o bloqueos
            if (usuario == null || usuario.Activo == false)
            {
                int tenantFallo = usuario?.TenantId ?? 0;
                int usuarioFalloId = usuario?.Id ?? 0;
                string causa = usuario == null ? "Correo no registrado" : "Cuenta deshabilitada";
                // Auditar y fallar handled below
            }
            else
            {
                var verificationResult = _passwordHasher.VerifyHashedPassword(usuario, usuario.PasswordHash, password);
                if (verificationResult != PasswordVerificationResult.Success)
                {
                    int tenantFallo = usuario.TenantId ?? 0;
                    int usuarioFalloId = usuario.Id;
                    string causa = "Contraseña incorrecta";
                    await _auditoriaService.RegistrarAsync(
                        tenantFallo,
                        usuarioFalloId,
                        "Login",
                        "0",
                        "Fallo Login",
                        $"Intento fallido de inicio de sesión para el correo '{email}'. Causa sospechosa: {causa}.",
                        CurrentUserIp
                    );
                    ModelState.AddModelError("", "Credenciales incorrectas o usuario deshabilitado.");
                    return View();
                }
            }



            // 🎯 CREACIÓN DE IDENTIDAD CON CLAIMS CRÍTICOS
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.Nombre),
                new Claim(ClaimTypes.Email, usuario.Email),
                new Claim(ClaimTypes.Role, usuario.Role.NombreRole),
                new Claim("TenantId", usuario.TenantId?.ToString() ?? "0")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = System.DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties
            );

            // 🪓 Se eliminó el registro de "Acceso Exitoso" para no llenar de basura la tabla.

            return RedirectToAction("Index", "Home");
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // 🪓 Se eliminó el registro de "Logout" para optimizar el almacenamiento.

            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpGet]
        [Route("barberia/{slugTenant}/Account/Register")]
        public async Task<IActionResult> Register(string slugTenant)
        {
            if (string.IsNullOrWhiteSpace(slugTenant))
            {
                return NotFound();
            }

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            // Se ignora filtros globales para poder resolver el tenant públicamente por su slug o subdominio
            var tenant = await _context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Slug == slugTenant || t.Subdominio == slugTenant);

            if (tenant == null)
            {
                return NotFound();
            }

            ViewBag.TenantId = tenant.Id;
            ViewBag.NombreNegocio = tenant.NombreNegocio;
            ViewBag.SlugTenant = slugTenant;
            ViewBag.ColorPrimario = tenant.ColorPrimario ?? "#1a1a1a";
            ViewBag.ColorSecundario = tenant.ColorSecundario ?? "#d4af37";
            ViewBag.RutaLogo = tenant.RutaLogo;

            var model = new RegisterViewModel
            {
                TenantId = tenant.Id,
                SlugTenant = slugTenant,
                NombreNegocio = tenant.NombreNegocio,
                ColorPrimario = tenant.ColorPrimario ?? "#1a1a1a",
                ColorSecundario = tenant.ColorSecundario ?? "#d4af37",
                RutaLogo = tenant.RutaLogo
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("barberia/{slugTenant}/Account/Register")]
        public async Task<IActionResult> Register(string slugTenant, RegisterViewModel model)
        {
            if (string.IsNullOrWhiteSpace(slugTenant))
            {
                return NotFound();
            }

            // Buscar tenant en base de datos ignorando filtros globales
            var tenant = await _context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Slug == slugTenant || t.Subdominio == slugTenant);

            if (tenant == null)
            {
                return NotFound();
            }

            // 🔒 ASIGNACIÓN ESTRICTA Y OBLIGATORIA DEL TENANTID (por debajo de cuerda)
            int tenantIdObligatorio = tenant.Id;
            model.TenantId = tenantIdObligatorio;
            model.SlugTenant = slugTenant;
            model.NombreNegocio = tenant.NombreNegocio;
            model.ColorPrimario = tenant.ColorPrimario ?? "#1a1a1a";
            model.ColorSecundario = tenant.ColorSecundario ?? "#d4af37";
            model.RutaLogo = tenant.RutaLogo;

            ViewBag.TenantId = tenant.Id;
            ViewBag.NombreNegocio = tenant.NombreNegocio;
            ViewBag.SlugTenant = slugTenant;
            ViewBag.ColorPrimario = tenant.ColorPrimario ?? "#1a1a1a";
            ViewBag.ColorSecundario = tenant.ColorSecundario ?? "#d4af37";
            ViewBag.RutaLogo = tenant.RutaLogo;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Validar unicidad del email en todo el sistema ignorando filtros de aislamiento
            bool emailExiste = await _context.Usuarios
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Email.ToLower() == model.Email.Trim().ToLower());

            if (emailExiste)
            {
                ModelState.AddModelError("Email", "Este correo electrónico ya se encuentra registrado.");
                return View(model);
            }

            // Validar unicidad del teléfono en caso de ser ingresado
            if (!string.IsNullOrWhiteSpace(model.Telefono))
            {
                bool telefonoExiste = await _context.Usuarios
                    .IgnoreQueryFilters()
                    .AnyAsync(u => u.Telefono == model.Telefono.Trim());

                if (telefonoExiste)
                {
                    ModelState.AddModelError("Telefono", "Este número de teléfono ya está registrado.");
                    return View(model);
                }
            }

            // Obtener rol "Cliente" para garantizar seguridad e integridad de roles
            var rolCliente = await _context.Roles
                .FirstOrDefaultAsync(r => r.NombreRole == "Cliente");

            if (rolCliente == null)
            {
                rolCliente = new Role { NombreRole = "Cliente" };
                _context.Roles.Add(rolCliente);
                await _context.SaveChangesAsync();
            }

            // Crear el nuevo usuario estrictamente amarrado a su sede correspondiente
            var nuevoUsuario = new Usuario
            {
                Nombre = model.Nombre.Trim(),
                Email = model.Email.Trim(),
                PasswordHash = _passwordHasher.HashPassword(null, model.Password),
                Telefono = string.IsNullOrWhiteSpace(model.Telefono) ? null : model.Telefono.Trim(),
                TenantId = tenantIdObligatorio,
                RoleId = rolCliente.Id,
                Activo = true,
                TokenUsado = false
            };

            _context.Usuarios.Add(nuevoUsuario);
            await _context.SaveChangesAsync();

            // Auditoría del registro de usuario en su respectivo Tenant
            await _auditoriaService.RegistrarAsync(
                tenantIdObligatorio,
                nuevoUsuario.Id,
                "Usuario",
                nuevoUsuario.Id.ToString(),
                "Registro Cliente",
                $"Nuevo cliente registrado en la sede '{tenant.NombreNegocio}' vía slug '{slugTenant}'. Email: {nuevoUsuario.Email}.",
                CurrentUserIp
            );

            // Iniciar sesión con claims de autenticación estándar del sistema
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, nuevoUsuario.Id.ToString()),
                new Claim(ClaimTypes.Name, nuevoUsuario.Nombre),
                new Claim(ClaimTypes.Email, nuevoUsuario.Email),
                new Claim(ClaimTypes.Role, rolCliente.NombreRole),
                new Claim("TenantId", nuevoUsuario.TenantId?.ToString() ?? "0")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = System.DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties
            );

            TempData["SuccessMessage"] = $"¡Registro exitoso! Bienvenido a {tenant.NombreNegocio}.";
            return RedirectToAction("Index", "Home");
        }
    }
}