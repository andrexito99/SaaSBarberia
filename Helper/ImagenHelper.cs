using System;
namespace SaaSBarberia.Helper
{
    public class ImagenHelper
    {
        public static string MostrarImagen(byte[]? imagenBytes)
        {
            if (imagenBytes != null && imagenBytes.Length > 0)
            {
                string base64 = Convert.ToBase64String(imagenBytes);
                return $"data:image/jpeg;base64,{base64}";
            }

            // Imagen de respaldo por si el usuario no subió ni m...
            return "/images/default-avatar.png";
        }
    }
}