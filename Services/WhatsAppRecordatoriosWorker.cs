namespace SaaSBarberia.Services;

public sealed class WhatsAppRecordatoriosWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WhatsAppRecordatoriosWorker> _logger;
    public WhatsAppRecordatoriosWorker(IServiceScopeFactory scopeFactory, ILogger<WhatsAppRecordatoriosWorker> logger) => (_scopeFactory, _logger) = (scopeFactory, logger);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try { using var scope = _scopeFactory.CreateScope(); await scope.ServiceProvider.GetRequiredService<IWhatsAppService>().ProcesarRecordatoriosAsync(DateTime.Now, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Error procesando recordatorios de WhatsApp."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
