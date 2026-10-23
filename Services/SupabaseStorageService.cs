using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

public class SupabaseStorageService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly string _bucketName = "uploads"; // Nombre del bucket en Supabase

    public SupabaseStorageService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    /// <summary>
    /// Sube un archivo (imagen) al bucket "uploads" de Supabase Storage usando la REST API pura.
    /// Devuelve la URL pública del archivo subido, o null si falla.
    /// </summary>
    /// <param name="archivo">El IFormFile recibido desde el formulario.</param>
    /// <param name="carpeta">Carpeta dentro del bucket (ej: "productos/tenant_5").</param>
    public async Task<string?> SubirImagenAsync(IFormFile archivo, string carpeta)
    {
        if (archivo == null || archivo.Length == 0) return null;

        try
        {
            // Leer la URL y la Key desde appsettings.json -> sección "Supabase"
            var supabaseConfigUrl = _configuration["Supabase:Url"]?.TrimEnd('/');
            var supabaseKey = _configuration["Supabase:Key"];

            if (string.IsNullOrEmpty(supabaseConfigUrl) || string.IsNullOrEmpty(supabaseKey))
            {
                Console.WriteLine("---> ❌ ERROR: Faltan las credenciales de Supabase en appsettings.json (Supabase:Url o Supabase:Key)");
                return null;
            }

            // 🔥 CORRECCIÓN CRÍTICA: Derivar la URL base del proyecto Supabase.
            // En appsettings.json el Url puede estar como "https://xxx.supabase.co/rest/v1/"
            // Necesitamos solo "https://xxx.supabase.co" para construir la ruta de Storage.
            var baseProjectUrl = supabaseConfigUrl;
            var restIndex = baseProjectUrl.IndexOf("/rest/", StringComparison.OrdinalIgnoreCase);
            if (restIndex > 0)
            {
                baseProjectUrl = baseProjectUrl.Substring(0, restIndex);
            }

            var extension = Path.GetExtension(archivo.FileName);
            var fileName = $"{carpeta}/{Guid.NewGuid()}{extension}";

            // Endpoint correcto de la REST API de Supabase Storage
            var endpoint = $"{baseProjectUrl}/storage/v1/object/{_bucketName}/{fileName}";

            Console.WriteLine($"---> 📤 SUBIENDO IMAGEN a: {endpoint}");

            using var memoryStream = new MemoryStream();
            await archivo.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint);
            // Autenticación con Bearer token + apikey (ambos requeridos por Supabase)
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supabaseKey);
            requestMessage.Headers.Add("apikey", supabaseKey);
            // 🔥 UPSERT: Permite sobrescribir archivos existentes sin error 409
            requestMessage.Headers.Add("x-upsert", "true");

            using var content = new ByteArrayContent(fileBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue(archivo.ContentType ?? "application/octet-stream");
            requestMessage.Content = content;

            var response = await _httpClient.SendAsync(requestMessage);

            if (response.IsSuccessStatusCode)
            {
                // URL pública correcta para guardar en la BD y mostrar en la interfaz
                var publicUrl = $"{baseProjectUrl}/storage/v1/object/public/{_bucketName}/{fileName}";
                Console.WriteLine($"---> 🚀 IMAGEN SUBIDA EXITOSAMENTE: {publicUrl}");
                return publicUrl;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"---> ❌ ERROR HTTP SUPABASE STORAGE ({response.StatusCode}): {errorContent}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"---> ❌ EXCEPCIÓN AL SUBIR IMAGEN: {ex.Message}");
            return null;
        }
    }
}