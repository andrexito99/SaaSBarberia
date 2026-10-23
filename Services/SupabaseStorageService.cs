using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

/// <summary>
/// Servicio para subir imágenes al bucket "uploads" de Supabase Storage
/// usando HttpClient puro y la REST API de Supabase Storage.
/// </summary>
public class SupabaseStorageService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private const string BucketName = "uploads";

    public SupabaseStorageService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    /// <summary>
    /// Sube un IFormFile al bucket "uploads" de Supabase Storage.
    /// Devuelve la URL pública permanente del archivo, o null si falla.
    /// </summary>
    /// <param name="archivo">Archivo recibido del formulario HTML (multipart/form-data).</param>
    /// <param name="carpeta">Subcarpeta dentro del bucket. Ejemplo: "productos/tenant_3"</param>
    public async Task<string?> SubirImagenAsync(IFormFile archivo, string carpeta)
    {
        if (archivo == null || archivo.Length == 0)
        {
            Console.WriteLine("---> ⚠️  [Storage] Archivo nulo o vacío, se omite la subida.");
            return null;
        }

        try
        {
            // ── 1. Leer credenciales desde appsettings.json ──────────────────────────
            // Preferimos "Supabase:ProjectUrl" (URL limpia). Si no existe, la derivamos
            // quitando el segmento "/rest/v1/" de "Supabase:Url".
            var projectUrl = _configuration["Supabase:ProjectUrl"]?.TrimEnd('/');

            if (string.IsNullOrEmpty(projectUrl))
            {
                var rawUrl = _configuration["Supabase:Url"]?.TrimEnd('/') ?? string.Empty;
                var restIdx = rawUrl.IndexOf("/rest/", StringComparison.OrdinalIgnoreCase);
                projectUrl = restIdx > 0 ? rawUrl[..restIdx] : rawUrl;
            }

            // La clave que se envía en los headers debe ser la anon/service_role key.
            // Intentamos primero "Supabase:AnonKey", luego "Supabase:Key".
            var apiKey = _configuration["Supabase:AnonKey"]
                      ?? _configuration["Supabase:Key"];

            if (string.IsNullOrEmpty(projectUrl) || string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("---> ❌ [Storage] FALTAN CREDENCIALES en appsettings.json. " +
                                  "Revisa Supabase:ProjectUrl y Supabase:AnonKey.");
                return null;
            }

            // ── 2. Construir la ruta y el endpoint ──────────────────────────────────
            var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            var objectPath = $"{carpeta.Trim('/')}/{Guid.NewGuid()}{extension}";
            var endpoint   = $"{projectUrl}/storage/v1/object/{BucketName}/{objectPath}";

            Console.WriteLine($"---> 📤 [Storage] Subiendo: {archivo.FileName} " +
                              $"({archivo.Length / 1024.0:F1} KB) → {endpoint}");

            // ── 3. Leer bytes del archivo ────────────────────────────────────────────
            byte[] fileBytes;
            using (var ms = new MemoryStream())
            {
                await archivo.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }

            // ── 4. Construir y enviar la petición HTTP POST ─────────────────────────
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);

            // Headers requeridos por Supabase Storage REST API
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Add("apikey", apiKey);
            // x-upsert: true → permite sobreescribir sin error 409 Conflict
            request.Headers.Add("x-upsert", "true");

            using var body = new ByteArrayContent(fileBytes);
            body.Headers.ContentType = new MediaTypeHeaderValue(
                string.IsNullOrWhiteSpace(archivo.ContentType)
                    ? "application/octet-stream"
                    : archivo.ContentType);
            request.Content = body;

            var response = await _httpClient.SendAsync(request);

            // ── 5. Evaluar la respuesta ──────────────────────────────────────────────
            if (response.IsSuccessStatusCode)
            {
                var publicUrl = $"{projectUrl}/storage/v1/object/public/{BucketName}/{objectPath}";
                Console.WriteLine($"---> ✅ [Storage] SUBIDA OK → {publicUrl}");
                return publicUrl;
            }
            else
            {
                // Impresión OBLIGATORIA del error completo para depuración
                var statusCode    = (int)response.StatusCode;
                var errorBody     = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"---> ❌ [Storage] FALLÓ HTTP {statusCode} ({response.StatusCode})");
                Console.WriteLine($"---> ❌ [Storage] Endpoint  : {endpoint}");
                Console.WriteLine($"---> ❌ [Storage] Respuesta : {errorBody}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"---> ❌ [Storage] EXCEPCIÓN: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }
}