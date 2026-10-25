
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SaaSBarberia.Data;
using SaaSBarberia.Services;
using System.Globalization;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

// Permitir el comportamiento heredado para DateTime (Local/Unspecified) in PostgreSQL
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

builder.Services.AddDbContext<BarberSaaSDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("con")));

// ☁️ CLOUDINARY: Servicio de almacenamiento de imágenes (reemplaza Supabase Storage)
// Registrado como Singleton porque Cloudinary es thread-safe y solo necesita una instancia.
builder.Services.AddHttpClient<SupabaseStorageService>();

// ─────────────────────────────────────────────────────────────────────────────
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
builder.Services.AddScoped<AgendaInteligenteService>();
builder.Services.AddScoped<HorarioLaboralService>();
builder.Services.AddScoped<IHerramientasService, HerramientasService>();
builder.Services.AddScoped<IProductosService, ProductosService>();
builder.Services.AddScoped<IAlquileresHerramientasService, AlquileresHerramientasService>();
builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();

builder.Services.AddScoped<INotificacionService, NotificacionService>();
builder.Services.AddScoped<IEventosSistemaService, EventosSistemaService>();
builder.Services.AddScoped<BarberoAusenciaService>();
builder.Services.AddScoped<WhatsAppConfiguracionService>();
builder.Services.AddScoped<WhatsAppPlantillaService>();
builder.Services.AddScoped<TenantModuloService>();
builder.Services.AddHttpClient<IWhatsAppService, WhatsAppService>();
builder.Services.AddHostedService<WhatsAppRecordatoriosWorker>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, TenantProvider>();

// Para evitar que los filtros automáticos bloqueen los arrays del formulario
builder.Services.AddControllersWithViews()
    .AddMvcOptions(options => {
        options.MaxModelValidationErrors = 50;
    });

var app = builder.Build();

// 🔥 FORZAR CULTURA INVARIANTE/ESPAÑOL PARA MAPEO DE TIMESPANS Y DECIMALES
var defaultCulture = new CultureInfo("es-CO");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(defaultCulture),
    SupportedCultures = new List<CultureInfo> { defaultCulture },
    SupportedUICultures = new List<CultureInfo> { defaultCulture }
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseDeveloperExceptionPage();
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();