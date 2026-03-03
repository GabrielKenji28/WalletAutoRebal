namespace AutoRebalCarteira.Data.Infrastructure.Cotahist;

public class CotacaoB3
{
    public DateTime DataPregao { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string CodigoBDI { get; set; } = string.Empty;
    public int TipoMercado { get; set; }
    public string NomeEmpresa { get; set; } = string.Empty;
    public decimal PrecoAbertura { get; set; }
    public decimal PrecoMaximo { get; set; }
    public decimal PrecoMinimo { get; set; }
    public decimal PrecoFechamento { get; set; }
    public decimal PrecoMedio { get; set; }
    public long QuantidadeNegociada { get; set; }
    public decimal VolumeNegociado { get; set; }
}
