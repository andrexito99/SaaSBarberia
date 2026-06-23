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

namespace SaaSBarberia.Controllers
{
    public class AccountController : Controller
    {
        private readonly BarberSaaSDbContext _context;
        private readonly IAuditoriaService _auditoriaService;

        public AccountController(BarberSaaSDbContext context, IAuditoriaService auditoriaService)
        {
            _context = context;
            _auditoriaService = auditoriaService;
        }

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
            if (usuario == null || usuario.PasswordHash != password || usuario.Activo == false)
            {
                int tenantFallo = usuario?.TenantId ?? 0;
                int usuarioFalloId = usuario?.Id ?? 0;
                string causa = usuario == null ? "Correo no registrado" : (usuario.Activo == false ? "Cuenta deshabilitada" : "Contraseña incorrecta");

                // 🔥 AUDITORÍA: Deja el rastro si intentan reventar el login
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
    }
}