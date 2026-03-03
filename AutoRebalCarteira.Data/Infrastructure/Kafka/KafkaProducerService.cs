using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AutoRebalCarteira.Data.Infrastructure.Kafka;

public interface IKafkaProducerService
{
    Task PublicarIRDedoDuroAsync(IRDedoDuroMessage message);
    Task PublicarIRVendaAsync(IRVendaMessage message);
}

public class KafkaProducerService : IKafkaProducerService, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducerService> _logger;
    private const string TopicIRDedoDuro = "ir-dedo-duro";
    private const string TopicIRVenda = "ir-venda";

    public KafkaProducerService(IConfiguration configuration, ILogger<KafkaProducerService> logger)
    {
        _logger = logger;
        var config = new ProducerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092"
        };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublicarIRDedoDuroAsync(IRDedoDuroMessage message)
    {
        try
        {
            var json = JsonSerializer.Serialize(message, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await _producer.ProduceAsync(TopicIRDedoDuro, new Message<string, string>
            {
                Key = message.ClienteId.ToString(),
                Value = json
            });
            _logger.LogInformation("IR Dedo-Duro publicado para cliente {ClienteId}, ticker {Ticker}", message.ClienteId, message.Ticker);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao publicar IR Dedo-Duro no Kafka para cliente {ClienteId}", message.ClienteId);
        }
    }

    public async Task PublicarIRVendaAsync(IRVendaMessage message)
    {
        try
        {
            var json = JsonSerializer.Serialize(message, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await _producer.ProduceAsync(TopicIRVenda, new Message<string, string>
            {
                Key = message.ClienteId.ToString(),
                Value = json
            });
            _logger.LogInformation("IR Venda publicado para cliente {ClienteId}, valor IR {ValorIR}", message.ClienteId, message.ValorIR);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao publicar IR Venda no Kafka para cliente {ClienteId}", message.ClienteId);
        }
    }

    public void Dispose()
    {
        _producer.Dispose();
    }
}

public class IRDedoDuroMessage
{
    public string Tipo { get; set; } = "IR_DEDO_DURO";
    public int ClienteId { get; set; }
    public string Cpf { get; set; } = string.Empty;
    public string Ticker { get; set; } = string.Empty;
    public string TipoOperacao { get; set; } = "COMPRA";
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal ValorOperacao { get; set; }
    public decimal Aliquota { get; set; } = 0.00005m;
    public decimal ValorIR { get; set; }
    public DateTime DataOperacao { get; set; }
}

public class IRVendaMessage
{
    public string Tipo { get; set; } = "IR_VENDA";
    public int ClienteId { get; set; }
    public string Cpf { get; set; } = string.Empty;
    public string MesReferencia { get; set; } = string.Empty;
    public decimal TotalVendasMes { get; set; }
    public decimal LucroLiquido { get; set; }
    public decimal Aliquota { get; set; } = 0.20m;
    public decimal ValorIR { get; set; }
    public List<DetalheVenda> Detalhes { get; set; } = [];
    public DateTime DataCalculo { get; set; }
}

public class DetalheVenda
{
    public string Ticker { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public decimal PrecoVenda { get; set; }
    public decimal PrecoMedio { get; set; }
    public decimal Lucro { get; set; }
}
