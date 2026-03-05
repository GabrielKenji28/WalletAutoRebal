using AutoRebalCarteira.Domain.Exceptions;

namespace AutoRebalCarteiraAPI.Services;

public class CompraProgramadaBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CompraProgramadaBackgroundService> _logger;

    public CompraProgramadaBackgroundService(IServiceProvider serviceProvider, ILogger<CompraProgramadaBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var agora = DateTime.Now;
            var hoje = DateOnly.FromDateTime(agora);

            if (EhDiaDeCompra(hoje) && agora.Hour >= 10)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var motorCompra = scope.ServiceProvider.GetRequiredService<IMotorCompraService>();
                    var resultado = await motorCompra.ExecutarCompraAsync(hoje);

                    if (resultado.Failed && resultado.ErrorCode == "COMPRA_JA_EXECUTADA")
                        _logger.LogDebug("Compra ja executada para {Data}", hoje);
                    else if (resultado.Failed)
                        _logger.LogWarning("Erro ao executar compra programada para {Data}: {Erro}", hoje, resultado.ErrorMessage);
                    else
                        _logger.LogInformation("Compra programada executada automaticamente para {Data}", hoje);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao executar compra programada para {Data}", hoje);
                }
            }

            // Verificar a cada hora
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private static bool EhDiaDeCompra(DateOnly data)
    {
        if (data.DayOfWeek == DayOfWeek.Saturday || data.DayOfWeek == DayOfWeek.Sunday)
            return false;

        // Dias 5, 15, 25 ou dia util subsequente
        var diasAlvo = new[] { 5, 15, 25 };

        foreach (var diaAlvo in diasAlvo)
        {
            var dataAlvo = new DateOnly(data.Year, data.Month, diaAlvo);
            // Ajustar para dia util
            while (dataAlvo.DayOfWeek == DayOfWeek.Saturday || dataAlvo.DayOfWeek == DayOfWeek.Sunday)
                dataAlvo = dataAlvo.AddDays(1);

            if (data == dataAlvo)
                return true;
        }

        return false;
    }
}
