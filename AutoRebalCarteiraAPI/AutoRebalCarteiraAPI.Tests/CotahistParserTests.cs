using AutoRebalCarteira.Data.Infrastructure.Cotahist;

namespace AutoRebalCarteiraAPI.Tests;

public class CotahistParserTests
{
    [Fact]
    public void ParseArquivo_DeveRetornarCotacoes()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var linhas = new[]
            {
                "00COTAHIST.2026 BOVESPA  20260225                                                                                                                                                                                                                    ",
                "01202602250200PETR4       010PETROBRAS   PN      N1   R$  0000000003520000000003650000000003480000000003560000000003580000000003570000000003590034561000000000150000000000000005376000000000000000000000000000000000000000BRPETRACNPR6180",
                "01202602250200VALE3       010VALE        ON      NM   R$  0000000006150000000006300000000006100000000006200000000006200000000006180000000006250025430000000000120000000000000007440000000000000000000000000000000000000000BRVALEACNOR0180",
                "99COTAHIST.2026 BOVESPA  2026022500000003                                                                                                                                                                                                        "
            };

            File.WriteAllLines(tempFile, linhas);

            var parser = new CotahistParser();
            var cotacoes = parser.ParseArquivo(tempFile).ToList();

            Assert.Equal(2, cotacoes.Count);
            Assert.Equal("PETR4", cotacoes[0].Ticker);
            Assert.Equal(35.80m, cotacoes[0].PrecoFechamento);
            Assert.Equal("VALE3", cotacoes[1].Ticker);
            Assert.Equal(62.00m, cotacoes[1].PrecoFechamento);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ParseArquivo_DeveIgnorarHeaderETrailer()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var linhas = new[]
            {
                "00COTAHIST.2026 BOVESPA  20260225                                                                                                                                                                                                                    ",
                "01202602250200PETR4       010PETROBRAS   PN      N1   R$  0000000003520000000003650000000003480000000003560000000003580000000003570000000003590034561000000000150000000000000005376000000000000000000000000000000000000000BRPETRACNPR6180",
                "99COTAHIST.2026 BOVESPA  2026022500000002                                                                                                                                                                                                        "
            };

            File.WriteAllLines(tempFile, linhas);

            var parser = new CotahistParser();
            var cotacoes = parser.ParseArquivo(tempFile).ToList();

            Assert.Single(cotacoes);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ParseArquivo_PrecoFechamento_DeveConverterCorretamente()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var linha = "01202602250200ITUB4       010ITAUUNIBANCOPN      N1   R$  0000000002950000000003050000000002920000000002980000000003000000000002990000000003010045670000000000180000000000000005400000000000000000000000000000000000000000BRITUBACNPR1180";

            File.WriteAllLines(tempFile, [
                "00COTAHIST.2026 BOVESPA  20260225                                                                                                                                                                                                                    ",
                linha,
                "99COTAHIST.2026 BOVESPA  2026022500000002                                                                                                                                                                                                        "
            ]);

            var parser = new CotahistParser();
            var cotacoes = parser.ParseArquivo(tempFile).ToList();

            Assert.Single(cotacoes);
            Assert.Equal("ITUB4", cotacoes[0].Ticker);
            Assert.Equal(30.00m, cotacoes[0].PrecoFechamento);
            Assert.Equal(29.50m, cotacoes[0].PrecoAbertura);
            Assert.Equal(30.50m, cotacoes[0].PrecoMaximo);
            Assert.Equal(29.20m, cotacoes[0].PrecoMinimo);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ObterCotacoesFechamento_PastaInexistente_DeveRetornarVazio()
    {
        var parser = new CotahistParser();
        var result = parser.ObterCotacoesFechamento("/pasta/inexistente", ["PETR4"]);
        Assert.Empty(result);
    }

    [Fact]
    public void ObterCotacoesFechamento_DeveRetornarCotacoes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var linhas = new[]
            {
                "00COTAHIST.2026 BOVESPA  20260225                                                                                                                                                                                                                    ",
                "01202602250200PETR4       010PETROBRAS   PN      N1   R$  0000000003520000000003650000000003480000000003560000000003580000000003570000000003590034561000000000150000000000000005376000000000000000000000000000000000000000BRPETRACNPR6180",
                "01202602250200VALE3       010VALE        ON      NM   R$  0000000006150000000006300000000006100000000006200000000006200000000006180000000006250025430000000000120000000000000007440000000000000000000000000000000000000000BRVALEACNOR0180",
                "99COTAHIST.2026 BOVESPA  2026022500000003                                                                                                                                                                                                        "
            };

            File.WriteAllLines(Path.Combine(tempDir, "COTAHIST_D20260225.TXT"), linhas);

            var parser = new CotahistParser();
            var cotacoes = parser.ObterCotacoesFechamento(tempDir, ["PETR4", "VALE3"]);

            Assert.Equal(2, cotacoes.Count);
            Assert.Equal(35.80m, cotacoes["PETR4"]);
            Assert.Equal(62.00m, cotacoes["VALE3"]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ParseArquivo_FiltroMercado_DeveExcluirOutrosMercados()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // Mercado a vista (010) deve ser incluido
            var linhaVista = "01202602250200PETR4       010PETROBRAS   PN      N1   R$  0000000003520000000003650000000003480000000003560000000003580000000003570000000003590034561000000000150000000000000005376000000000000000000000000000000000000000BRPETRACNPR6180";
            // Mercado fracionario (020) deve ser incluido
            var linhaFrac  = "01202602259600PETR4F      020PETROBRAS   PN      N1   R$  0000000003520000000003650000000003480000000003560000000003580000000003570000000003590001234000000000005000000000000000179000000000000000000000000000000000000000BRPETRACNPR6180";
            // Mercado a termo (030) deve ser excluido
            var linhaTermo = "01202602250200PETR4       030PETROBRAS   PN      N1   R$  0000000003520000000003650000000003480000000003560000000003580000000003570000000003590034561000000000150000000000000005376000000000000000000000000000000000000000BRPETRACNPR6180";

            File.WriteAllLines(tempFile, [
                "00COTAHIST.2026 BOVESPA  20260225                                                                                                                                                                                                                    ",
                linhaVista,
                linhaFrac,
                linhaTermo,
                "99COTAHIST.2026 BOVESPA  2026022500000004                                                                                                                                                                                                        "
            ]);

            var parser = new CotahistParser();
            var cotacoes = parser.ParseArquivo(tempFile).ToList();

            Assert.Equal(2, cotacoes.Count);
            Assert.Contains(cotacoes, c => c.TipoMercado == 10);
            Assert.Contains(cotacoes, c => c.TipoMercado == 20);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
