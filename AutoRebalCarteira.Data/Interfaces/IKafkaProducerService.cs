namespace AutoRebalCarteira.Data.Interfaces;

public interface IKafkaProducerService
{
    Task PublicarIRDedoDuroAsync(Infrastructure.Kafka.IRDedoDuroMessage message);
    Task PublicarIRVendaAsync(Infrastructure.Kafka.IRVendaMessage message);
}
