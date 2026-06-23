using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SaaSBarberia.Services;

namespace SaaSBarberia.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IAuditoriaService auditoriaService)
        {
            try
            {
                // Continúa el camino normal de la petición (deja que los controladores hagan su trabajo)
                await _next(context);
            }
            catch (Exception ex)
            {
                // 🚨 ¡ALERTA! El colador atrapó un error en alguna parte del sistema
                _logger.LogError(ex, $"Ocurrió un error no controlado en el SaaS: {ex.Message}");

                await RegistrarErrorEnAuditoriaAsync(context, auditoriaService, ex);

                // Redirigir al usuario a una página amigable de error para que no vea código feo
                context.Response.Redirect("/Home/Error");
            }
        }

        private async Task RegistrarErrorEnAuditoriaAsync(HttpContext context, IAuditoriaService auditoriaService, Exception ex)
        {
            try
            {
                // Extraer datos de seguridad del contexto actual
                string usuarioIdStr = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int usuarioId = int.TryParse(usuarioIdStr, out int id) ? id : 0;

                string tenantIdStr = context.User?.FindFirst("TenantId")?.Value;
                int tenantId = int.Parse(tenantIdStr ?? "0");

                string ip = context.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

                // Cortar la descripción del error para que quepa bien en la base de datos si es muy larga
                string mensajeCorta = ex.Message.Length > 200 ? ex.Message.Substring(0, 200) : ex.Message;
                string detalleCompleto = $"[ERROR CRÍTICO GLOBAL] Mensaje: {mensajeCorta}. Origen: {ex.Source}. StackTrace: {ex.StackTrace}";

                // 🔥 Guardamos el chisme del error automáticamente en tu tabla de Auditoria
                await auditoriaService.RegistrarAsync(
                    tenantId,
                    usuarioId,
                    "Sistema/Crash",
                    "0",
                    "Error Interno",
                    detalleCompleto,
                    ip
                );
            }
            catch
            {
                // Si falla la auditoría (ej. se cayó la BD), que no se rompa el middleware principal
            }
        }
    }
}