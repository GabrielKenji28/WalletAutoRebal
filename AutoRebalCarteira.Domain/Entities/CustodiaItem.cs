namespace AutoRebalCarteira.Domain.Entities;

public class CustodiaItem
{
    public int Id { get; set; }
    public int ContaGraficaId { get; set; }
    public ContaGrafica ContaGrafica { get; set; } = null!;
    public string Ticker { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public decimal PrecoMedio { get; set; }
}
