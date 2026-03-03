namespace AutoRebalCarteira.Domain.Entities;

public class Distribuicao
{
    public int Id { get; set; }
    public int OrdemCompraId { get; set; }
    public OrdemCompra OrdemCompra { get; set; } = null!;
    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;
    public decimal ValorAporte { get; set; }
    public DateTime DataDistribuicao { get; set; }

    public ICollection<DistribuicaoItem> Itens { get; set; } = [];
}
