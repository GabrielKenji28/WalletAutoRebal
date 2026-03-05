namespace AutoRebalCarteira.Domain.Entities;

public class ContaGrafica : EntidadeBase
{
    public string NumeroConta { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }

    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public ICollection<CustodiaItem> Custodia { get; set; } = [];
}
