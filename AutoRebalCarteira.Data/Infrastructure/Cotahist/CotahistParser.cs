using System.Globalization;
using System.Text;

namespace AutoRebalCarteira.Data.Infrastructure.Cotahist;

public class CotahistParser
{
    public IEnumerable<CotacaoB3> ParseArquivo(string caminhoArquivo)
    {
        var cotacoes = new List<CotacaoB3>();

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding("ISO-8859-1");

        foreach (var linha in File.ReadLines(caminhoArquivo, encoding))
        {
            if (linha.Length < 245)
                continue;

            var tipoRegistro = linha.Substring(0, 2);
            if (tipoRegistro != "01")
                continue;

            var tipoMercado = int.Parse(linha.Substring(24, 3).Trim());

            // Filtrar apenas mercado a vista (010) e fracionario (020)
            if (tipoMercado != 10 && tipoMercado != 20)
                continue;

            var cotacao = new CotacaoB3
            {
                DataPregao = DateTime.ParseExact(
                    linha.Substring(2, 8), "yyyyMMdd",
                    CultureInfo.InvariantCulture),
                CodigoBDI = linha.Substring(10, 2).Trim(),
                Ticker = linha.Substring(12, 12).Trim(),
                TipoMercado = tipoMercado,
                NomeEmpresa = linha.Substring(27, 12).Trim(),
                PrecoAbertura = ParsePreco(linha.Substring(56, 13)),
                PrecoMaximo = ParsePreco(linha.Substring(69, 13)),
                PrecoMinimo = ParsePreco(linha.Substring(82, 13)),
                PrecoMedio = ParsePreco(linha.Substring(95, 13)),
                PrecoFechamento = ParsePreco(linha.Substring(108, 13)),
                QuantidadeNegociada = long.Parse(linha.Substring(152, 18).Trim()),
                VolumeNegociado = ParsePreco(linha.Substring(170, 18))
            };

            cotacoes.Add(cotacao);
        }

        return cotacoes;
    }

    public CotacaoB3? ObterCotacaoFechamento(string pastaCotacoes, string ticker)
    {
        if (!Directory.Exists(pastaCotacoes))
            return null;

        var arquivos = Directory.GetFiles(pastaCotacoes, "COTAHIST_D*.TXT")
            .OrderByDescending(f => f)
            .ToList();

        foreach (var arquivo in arquivos)
        {
            var cotacoes = ParseArquivo(arquivo);
            var cotacao = cotacoes
                .Where(c => c.Ticker.Equals(ticker, StringComparison.OrdinalIgnoreCase))
                .Where(c => c.TipoMercado == 10) // Mercado a vista
                .FirstOrDefault();

            if (cotacao != null)
                return cotacao;
        }

        return null;
    }

    public Dictionary<string, decimal> ObterCotacoesFechamento(string pastaCotacoes, IEnumerable<string> tickers)
    {
        var resultado = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(pastaCotacoes))
            return resultado;

        var arquivos = Directory.GetFiles(pastaCotacoes, "COTAHIST_D*.TXT")
            .OrderByDescending(f => f)
            .ToList();

        var tickersRestantes = new HashSet<string>(tickers, StringComparer.OrdinalIgnoreCase);

        foreach (var arquivo in arquivos)
        {
            if (tickersRestantes.Count == 0)
                break;

            var cotacoes = ParseArquivo(arquivo);
            foreach (var cotacao in cotacoes.Where(c => c.TipoMercado == 10 && c.CodigoBDI == "02"))
            {
                if (tickersRestantes.Contains(cotacao.Ticker))
                {
                    resultado[cotacao.Ticker] = cotacao.PrecoFechamento;
                    tickersRestantes.Remove(cotacao.Ticker);
                }
            }
        }

        return resultado;
    }

    private static decimal ParsePreco(string valorBruto)
    {
        if (long.TryParse(valorBruto.Trim(), out var valor))
            return valor / 100m;
        return 0m;
    }
}
