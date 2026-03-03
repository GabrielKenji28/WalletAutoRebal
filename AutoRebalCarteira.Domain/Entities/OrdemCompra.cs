namespace AutoRebalCarteira.Domain.Entities;

public class OrdemCompra
{
    public int Id { get; set; }
    public DateTime DataExecucao { get; set; }
    public decimal ValorTotalConsolidado { get; set; }
    public int TotalClientes { get; set; }

    public ICollection<OrdemCompraItem> Itens { get; set; } = [];
    public ICollection<Distribuicao> Distribuicoes { get; set; } = [];
}
