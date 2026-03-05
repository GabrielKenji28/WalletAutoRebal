namespace AutoRebalCarteira.Domain.Entities;

public class HistoricoValorMensal : EntidadeBase
{
    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;
    public decimal ValorAnterior { get; set; }
    public decimal ValorNovo { get; set; }
    public DateTime DataAlteracao { get; set; }
}
