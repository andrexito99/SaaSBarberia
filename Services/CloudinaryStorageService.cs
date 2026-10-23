using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SaaSBarberia.Services
{
    /// <summary>
    /// Servicio de subida de imágenes usando Cloudinary SDK oficial.
    /// Reemplaza al SupabaseStorageService. Registrado como Singleton en Program.cs.
    /// </summary>
    public class CloudinaryStorageService
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryStorageService(IConfiguration configuration)
        {
            var cloudName  = configuration["Cloudinary:CloudName"]  ?? throw new InvalidOperationException("Falta Cloudinary:CloudName en appsettings.json");
            var apiKey     = configuration["Cloudinary:ApiKey"]     ?? throw new InvalidOperationException("Falta Cloudinary:ApiKey en appsettings.json");
            var apiSecret  = configuration["Cloudinary:ApiSecret"]  ?? throw new InvalidOperationException("Falta Cloudinary:ApiSecret en appsettings.json");

            var account    = new Account(cloudName, apiKey, apiSecret);
            _cloudinary    = new Cloudinary(account);
            _cloudinary.Api.Secure = true; // Forzar HTTPS en todas las URLs
        }

        /// <summary>
        /// Sube un IFormFile a Cloudinary en la carpeta especificada.
        /// Devuelve la URL pública segura (HTTPS), o null si falla.
        /// </summary>
        /// <param name="archivo">Archivo recibido del formulario (multipart/form-data).</param>
        /// <param name="carpeta">
        /// Carpeta de destino en Cloudinary con aislamiento multi-tenant.
        /// Ejemplos: "productos/tenant_3", "barberos/tenant_5", "tenants/tenant_2"
        /// </param>
        public async Task<string?> SubirImagenAsync(IFormFile archivo, string carpeta)
        {
            if (archivo == null || archivo.Length == 0)
            {
                Console.WriteLine("---> ⚠️  [Cloudinary] Archivo nulo o vacío, se omite la subida.");
                return null;
            }

            try
            {
                Console.WriteLine($"---> 📤 [Cloudinary] Subiendo: {archivo.FileName} " +
                                  $"({archivo.Length / 1024.0:F1} KB) → carpeta: {carpeta}");

                using var stream = archivo.OpenReadStream();

                var uploadParams = new ImageUploadParams
                {
                    File         = new FileDescription(archivo.FileName, stream),
                    Folder       = carpeta.Trim('/'),               // Carpeta con aislamiento por TenantId
                    Overwrite    = true,                            // Equivalente a x-upsert:true
                    UniqueFilename = true,                          // Nombre único con UUID automático
                    UseFilename  = false,                           // No usar el nombre original (evita colisiones)
                };

                var result = await _cloudinary.UploadAsync(uploadParams);

                if (result.Error != null)
                {
                    Console.WriteLine($"---> ❌ [Cloudinary] ERROR: {result.Error.Message}");
                    return null;
                }

                var url = result.SecureUrl.ToString();
                Console.WriteLine($"---> ✅ [Cloudinary] SUBIDA OK → {url}");
                return url;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"---> ❌ [Cloudinary] EXCEPCIÓN: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }
    }
}
