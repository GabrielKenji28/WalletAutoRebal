using AutoRebalCarteiraAPI.DTOs;
using AutoRebalCarteiraAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace AutoRebalCarteiraAPI.Controllers;

[ApiController]
[Route("api/motor")]
public class MotorController : ControllerBase
{
    private readonly IMotorCompraService _motorCompraService;
    private readonly IMotorRebalanceamentoService _rebalanceamentoService;

    public MotorController(IMotorCompraService motorCompraService, IMotorRebalanceamentoService rebalanceamentoService)
    {
        _motorCompraService = motorCompraService;
        _rebalanceamentoService = rebalanceamentoService;
    }

    [HttpPost("executar-compra")]
    [ProducesResponseType(typeof(ExecutarCompraResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ExecutarCompra([FromBody] ExecutarCompraRequest request)
    {
        var data = DateOnly.Parse(request.DataReferencia);
        var response = await _motorCompraService.ExecutarCompraAsync(data);
        return Ok(response);
    }

    [HttpPost("rebalancear-desvio")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RebalancearPorDesvio()
    {
        await _rebalanceamentoService.RebalancearPorDesvioProporcaoAsync();
        return Ok(new { mensagem = "Rebalanceamento por desvio de proporcao executado com sucesso." });
    }
}
