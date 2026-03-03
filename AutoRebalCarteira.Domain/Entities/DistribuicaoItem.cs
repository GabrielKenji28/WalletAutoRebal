namespace AutoRebalCarteira.Domain.Entities;

public class DistribuicaoItem
{
    public int Id { get; set; }
    public int DistribuicaoId { get; set; }
    public Distribuicao Distribuicao { get; set; } = null!;
    public string Ticker { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
}
