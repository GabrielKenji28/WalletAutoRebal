using AutoRebalCarteira.Domain.Exceptions;

namespace AutoRebalCarteiraAPI.Tests;

public class BusinessExceptionTests
{
    [Fact]
    public void BusinessException_DeveConterCodigoEStatusCode()
    {
        var ex = new BusinessException("Mensagem de erro", "CODIGO_ERRO", 404);

        Assert.Equal("Mensagem de erro", ex.Message);
        Assert.Equal("CODIGO_ERRO", ex.Codigo);
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public void BusinessException_StatusCodePadrao_Deve400()
    {
        var ex = new BusinessException("Erro", "CODIGO");

        Assert.Equal(400, ex.StatusCode);
    }
}
