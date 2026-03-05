namespace AutoRebalCarteira.Domain.Entities;

public class Cliente : EntidadeBase
{
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal ValorMensal { get; set; }
    public DateTime DataAdesao { get; set; }
    public DateTime? DataSaida { get; set; }

    public ContaGrafica ContaGrafica { get; set; } = null!;
    public ICollection<HistoricoValorMensal> HistoricoValores { get; set; } = [];
}
