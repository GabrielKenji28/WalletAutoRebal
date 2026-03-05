namespace AutoRebalCarteira.Domain.Entities;

public abstract class EntidadeBase
{
    public int Id { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public bool Ativo { get; set; } = true;
}
