using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Linq;
using System.Security.Claims;

namespace SaaSBarberia.Filters
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class TenantAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;

            // 1. Si no está autenticado, que el middleware normal se encargue o rebote
            if (user.Identity == null || !user.Identity.IsAuthenticated) return;

            // 2. Si es SuperAdmin, tiene pase libre a TODAS las sedes/tenants
            if (user.IsInRole("SuperAdmin")) return;

            // 3. Extraer el TenantId del usuario logueado desde sus Claims
            var userTenantClaim = user.FindFirst("TenantId")?.Value;
            if (string.IsNullOrEmpty(userTenantClaim) || !int.TryParse(userTenantClaim, out int userTenantId))
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                return;
            }

            // 4. VALIDACIÓN DE SEGURIDAD DE RUTA O FORMULARIO
            // Buscamos si en los parámetros de la URL (?tenantId=2 o /Editar/2) o del formulario mandaron un TenantId
            int targetTenantId = 0;

            if (context.RouteData.Values.TryGetValue("tenantId", out var routeTenant))
            {
                int.TryParse(routeTenant?.ToString(), out targetTenantId);
            }
            else if (context.HttpContext.Request.Query.TryGetValue("tenantId", out var queryTenant))
            {
                int.TryParse(queryTenant.FirstOrDefault(), out targetTenantId);
            }
            else if (context.HttpContext.Request.HasFormContentType && context.HttpContext.Request.Form.TryGetValue("TenantId", out var formTenant))
            {
                int.TryParse(formTenant.FirstOrDefault(), out targetTenantId);
            }

            // Si intentaron mandar un TenantId que no es el del usuario, ¡bloqueo inmediato!
            if (targetTenantId != 0 && targetTenantId != userTenantId)
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
            }
        }
    }
}