using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Models;
using System.Diagnostics;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SaaSBarberia.Controllers
{
    public class HomeController : Controller
    {
        private readonly BarberSaaSDbContext _context;

        public HomeController(BarberSaaSDbContext context)
        {
            _context = context;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                // 🌟 NOTA TÉCNICA: Para evitar el error de "View Not Found", renderizamos la vista Index.cshtml, 
                // y mediante condicionales de C# (@if) inyectamos el HTML del Dashboard correspondiente a cada rol.
                return View("Index");
            }

            // Si no está logueado, le cargamos el Catálogo Público de Servicios
            var serviciosPublicos = await _context.Servicios.Include(s => s.Tenant).ToListAsync();
            return View("IndexPublico", serviciosPublicos);
        }

        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}