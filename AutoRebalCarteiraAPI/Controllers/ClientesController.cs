using AutoRebalCarteiraAPI.DTOs;
using AutoRebalCarteiraAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace AutoRebalCarteiraAPI.Controllers;

[ApiController]
[Route("api/clientes")]
public class ClientesController : ControllerBase
{
    private readonly IClienteService _clienteService;

    public ClientesController(IClienteService clienteService)
    {
        _clienteService = clienteService;
    }

    [HttpPost("adesao")]
    [ProducesResponseType(typeof(AdesaoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Aderir([FromBody] AdesaoRequest request)
    {
        var response = await _clienteService.AderirAsync(request);
        return CreatedAtAction(nameof(ConsultarCarteira), new { clienteId = response.ClienteId }, response);
    }

    [HttpPost("{clienteId}/saida")]
    [ProducesResponseType(typeof(SaidaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Sair(int clienteId)
    {
        var response = await _clienteService.SairAsync(clienteId);
        return Ok(response);
    }

    [HttpPut("{clienteId}/valor-mensal")]
    [ProducesResponseType(typeof(AlterarValorMensalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AlterarValorMensal(int clienteId, [FromBody] AlterarValorMensalRequest request)
    {
        var response = await _clienteService.AlterarValorMensalAsync(clienteId, request);
        return Ok(response);
    }

    [HttpGet("{clienteId}/carteira")]
    [ProducesResponseType(typeof(CarteiraResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConsultarCarteira(int clienteId)
    {
        var response = await _clienteService.ConsultarCarteiraAsync(clienteId);
        return Ok(response);
    }

    [HttpGet("{clienteId}/rentabilidade")]
    [ProducesResponseType(typeof(RentabilidadeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConsultarRentabilidade(int clienteId)
    {
        var response = await _clienteService.ConsultarRentabilidadeAsync(clienteId);
        return Ok(response);
    }
}
