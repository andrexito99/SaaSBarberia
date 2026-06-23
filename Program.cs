using Microsoft.AspNetCore.Authentication.Cookies; 
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<BarberSaaSDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("con")));


builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login"; 
        options.AccessDeniedPath = "/Account/AccessDenied"; 
        options.ExpireTimeSpan = TimeSpan.FromHours(8); 
    });

builder.Services.AddScoped<TenantService>();
builder.Services.AddScoped<UsuarioService>();
builder.Services.AddScoped<CitaService>();
builder.Services.AddScoped<ServicioService>();
builder.Services.AddScoped<BarberoService>();
builder.Services.AddScoped<FacturaService>();
builder.Services.AddScoped<CajaService>();
builder.Services.AddScoped<IHerramientasService, HerramientasService>();
builder.Services.AddScoped<IProductosService, ProductosService>();
builder.Services.AddScoped<IAlquileresHerramientasService, AlquileresHerramientasService>();
builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseMiddleware<SaaSBarberia.Middleware.ExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseRouting();

// 🌟 NUEVO: Obligatorio ponerlo antes de Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();