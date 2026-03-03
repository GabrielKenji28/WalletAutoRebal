namespace AutoRebalCarteira.Domain.Entities;

public class CestaRecomendacao
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativa { get; set; } = true;
    public DateTime DataCriacao { get; set; }
    public DateTime? DataDesativacao { get; set; }

    public ICollection<CestaItem> Itens { get; set; } = [];
}
