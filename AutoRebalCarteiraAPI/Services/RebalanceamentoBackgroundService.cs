using System.Threading.Channels;
using AutoRebalCarteiraAPI.Interfaces;

namespace AutoRebalCarteiraAPI.Services;

public record RebalanceamentoCestaCommand(int CestaAntigaId, int CestaNovaId);

public class RebalanceamentoChannel : IRebalanceamentoChannel
{
    private readonly Channel<RebalanceamentoCestaCommand> _channel =
        Channel.CreateUnbounded<RebalanceamentoCestaCommand>();

    public ChannelReader<RebalanceamentoCestaCommand> Reader => _channel.Reader;

    public ValueTask EnfileirarAsync(RebalanceamentoCestaCommand command)
        => _channel.Writer.WriteAsync(command);
}

public class RebalanceamentoBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RebalanceamentoChannel _channel;
    private readonly ILogger<RebalanceamentoBackgroundService> _logger;

    public RebalanceamentoBackgroundService(
        IServiceProvider serviceProvider,
        RebalanceamentoChannel channel,
        ILogger<RebalanceamentoBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _channel = channel;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var command in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var rebalService = scope.ServiceProvider.GetRequiredService<IMotorRebalanceamentoService>();
                await rebalService.RebalancearPorMudancaCestaAsync(command.CestaAntigaId, command.CestaNovaId);
                _logger.LogInformation(
                    "Rebalanceamento por mudanca de cesta concluido: cesta {Antiga} -> {Nova}",
                    command.CestaAntigaId, command.CestaNovaId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Erro ao processar rebalanceamento: cesta {Antiga} -> {Nova}",
                    command.CestaAntigaId, command.CestaNovaId);
            }
        }
    }
}
