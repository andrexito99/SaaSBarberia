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

    public async Task<string?> SubirImagenAsync(IFormFile archivo, string carpeta)
    {
        if (archivo == null || archivo.Length == 0) return null;

        try
        {
            var supabaseUrl = _configuration["Supabase:Url"]?.TrimEnd('/');
            var supabaseKey = _configuration["Supabase:Key"];

            if (string.IsNullOrEmpty(supabaseUrl) || string.IsNullOrEmpty(supabaseKey))
            {
                Console.WriteLine("---> ❌ ERROR: Faltan las credenciales de Supabase en appsettings.json");
                return null;
            }

            var extension = Path.GetExtension(archivo.FileName);
            var fileName = $"{carpeta}/{Guid.NewGuid()}{extension}";

            // Endpoint de la REST API de Supabase Storage
            var endpoint = $"{supabaseUrl}/storage/v1/object/{_bucketName}/{fileName}";

            using var memoryStream = new MemoryStream();
            await archivo.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supabaseKey);
            requestMessage.Headers.Add("apikey", supabaseKey);

            using var content = new ByteArrayContent(fileBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue(archivo.ContentType ?? "application/octet-stream");
            requestMessage.Content = content;

            var response = await _httpClient.SendAsync(requestMessage);

            if (response.IsSuccessStatusCode)
            {
                // URL pública lista para la BD
                var publicUrl = $"{supabaseUrl}/storage/v1/object/public/{_bucketName}/{fileName}";
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