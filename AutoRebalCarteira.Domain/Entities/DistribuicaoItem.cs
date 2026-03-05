namespace AutoRebalCarteira.Domain.Entities;

public class DistribuicaoItem : EntidadeBase
{
    public int DistribuicaoId { get; set; }
    public Distribuicao Distribuicao { get; set; } = null!;
    public string Ticker { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
}
