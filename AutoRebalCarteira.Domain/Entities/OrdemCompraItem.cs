namespace AutoRebalCarteira.Domain.Entities;

public class OrdemCompraItem
{
    public int Id { get; set; }
    public int OrdemCompraId { get; set; }
    public OrdemCompra OrdemCompra { get; set; } = null!;
    public string Ticker { get; set; } = string.Empty;
    public int QuantidadeLotePadrao { get; set; }
    public int QuantidadeFracionario { get; set; }
    public int QuantidadeTotal { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal ValorTotal { get; set; }
}
