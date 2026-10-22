using Supabase;
using Microsoft.AspNetCore.Http;

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

        // Generar un nombre único para el archivo para evitar duplicados o sobreescrituras
        var fileName = $"{carpeta}/{Guid.NewGuid()}_{Path.GetFileName(archivo.FileName)}";

        using var memoryStream = new MemoryStream();
        await archivo.CopyToAsync(memoryStream);
        var fileBytes = memoryStream.ToArray();

        // Subir los bytes al Storage de Supabase
        var options = new Supabase.Storage.FileOptions { ContentType = archivo.ContentType };
        await _supabaseClient.Storage
            .From(_bucketName)
            .Upload(fileBytes, fileName, options);

        // Devolver la URL pública lista para guardarla en la Base de Datos
        return _supabaseClient.Storage
            .From(_bucketName)
            .GetPublicUrl(fileName);
    }
}