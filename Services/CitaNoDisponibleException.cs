namespace SaaSBarberia.Services;

public sealed class CitaNoDisponibleException : Exception
{
    public CitaNoDisponibleException(string message) : base(message)
    {
    }
}
