using Supabase;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;

public class SupabaseStorageService
{
    private readonly Supabase.Client _supabaseClient;
    private readonly string _bucketName = "uploads"; // El nombre del Bucket público que creaste en Supabase

    public SupabaseStorageService(Supabase.Client supabaseClient)
    {
        _supabaseClient = supabaseClient;
    }

    public async Task<string?> SubirImagenAsync(IFormFile archivo, string carpeta)
    {
        if (archivo == null || archivo.Length == 0) return null;

        try
        {
            // Limpiar nombre y generar un nombre único seguro
            var extension = Path.GetExtension(archivo.FileName);
            var fileName = $"{carpeta}/{Guid.NewGuid()}{extension}";

            using var memoryStream = new MemoryStream();
            await archivo.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            // Configurar opciones de subida con Upsert para evitar conflictos de sobreescritura
            var options = new Supabase.Storage.FileOptions
            {
                ContentType = archivo.ContentType,
                Upsert = true
            };

            // Subir los bytes al Storage de Supabase
            await _supabaseClient.Storage
                .From(_bucketName)
                .Upload(fileBytes, fileName, options);

            // Obtener la URL pública generada
            var publicUrl = _supabaseClient.Storage
                .From(_bucketName)
                .GetPublicUrl(fileName);

            Console.WriteLine($"---> IMAGEN SUBIDA EXITOSAMENTE A SUPABASE: {publicUrl}");
            return publicUrl;
        }
        catch (Exception ex)
        {
            // 🔥 Esto te dirá exactamente en los logs de Render si Supabase rechazó la subida por permisos o credenciales
            Console.WriteLine($"---> ERROR CRÍTICOS AL SUBIR A SUPABASE STORAGE: {ex.Message} | Inner: {ex.InnerException?.Message}");
            return null;
        }
    }
}