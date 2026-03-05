namespace AutoRebalCarteira.Domain.Entities;

public class CestaItem : EntidadeBase
{
    public int CestaRecomendacaoId { get; set; }
    public CestaRecomendacao CestaRecomendacao { get; set; } = null!;
    public string Ticker { get; set; } = string.Empty;
    public decimal Percentual { get; set; }
}
