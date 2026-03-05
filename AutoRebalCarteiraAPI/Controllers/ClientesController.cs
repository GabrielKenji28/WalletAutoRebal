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
    [ProducesResponseType(typeof(AdesaoResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Aderir([FromBody] AdesaoRequest request)
    {
        var response = await _clienteService.AderirAsync(request);
        if (response.Failed)
            return StatusCode(response.StatusCode, response);
        return CreatedAtAction(nameof(ConsultarCarteira), new { clienteId = response.ClienteId }, response);
    }

    [HttpPost("{clienteId}/saida")]
    [ProducesResponseType(typeof(SaidaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SaidaResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Sair(int clienteId)
    {
        var response = await _clienteService.SairAsync(clienteId);
        if (response.Failed)
            return StatusCode(response.StatusCode, response);
        return Ok(response);
    }

    [HttpPut("{clienteId}/valor-mensal")]
    [ProducesResponseType(typeof(AlterarValorMensalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AlterarValorMensalResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AlterarValorMensalResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AlterarValorMensal(int clienteId, [FromBody] AlterarValorMensalRequest request)
    {
        var response = await _clienteService.AlterarValorMensalAsync(clienteId, request);
        if (response.Failed)
            return StatusCode(response.StatusCode, response);
        return Ok(response);
    }

    [HttpGet("{clienteId}/carteira")]
    [ProducesResponseType(typeof(CarteiraResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(CarteiraResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConsultarCarteira(int clienteId)
    {
        var response = await _clienteService.ConsultarCarteiraAsync(clienteId);
        if (response.Failed)
            return StatusCode(response.StatusCode, response);
        return Ok(response);
    }

    [HttpGet("{clienteId}/rentabilidade")]
    [ProducesResponseType(typeof(RentabilidadeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RentabilidadeResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConsultarRentabilidade(int clienteId)
    {
        var response = await _clienteService.ConsultarRentabilidadeAsync(clienteId);
        if (response.Failed)
            return StatusCode(response.StatusCode, response);
        return Ok(response);
    }
}
