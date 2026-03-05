namespace AutoRebalCarteira.Domain.Entities;

public class CestaRecomendacao : EntidadeBase
{
    public string Nome { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; }
    public DateTime? DataDesativacao { get; set; }

    public ICollection<CestaItem> Itens { get; set; } = [];
}
