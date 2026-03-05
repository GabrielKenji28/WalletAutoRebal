using AutoRebalCarteiraAPI.DTOs;
using AutoRebalCarteiraAPI.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AutoRebalCarteiraAPI.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly ICestaService _cestaService;
    private readonly ICustodiaMasterService _custodiaMasterService;

    public AdminController(ICestaService cestaService, ICustodiaMasterService custodiaMasterService)
    {
        _cestaService = cestaService;
        _custodiaMasterService = custodiaMasterService;
    }

    [HttpPost("cesta")]
    [ProducesResponseType(typeof(CadastrarCestaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(CadastrarCestaResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CadastrarCesta([FromBody] CadastrarCestaRequest request)
    {
        var response = await _cestaService.CadastrarOuAlterarAsync(request);
        if (response.Failed)
            return StatusCode(response.StatusCode, response);
        return CreatedAtAction(nameof(ObterCestaAtual), null, response);
    }

    [HttpGet("cesta/atual")]
    [ProducesResponseType(typeof(CestaAtualResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(CestaAtualResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterCestaAtual()
    {
        var response = await _cestaService.ObterCestaAtualAsync();
        if (response.Failed)
            return StatusCode(response.StatusCode, response);
        return Ok(response);
    }

    [HttpGet("cesta/historico")]
    [ProducesResponseType(typeof(HistoricoCestasResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterHistoricoCestas()
    {
        var response = await _cestaService.ObterHistoricoAsync();
        return Ok(response);
    }

    [HttpGet("conta-master/custodia")]
    [ProducesResponseType(typeof(CustodiaMasterResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterCustodiaMaster()
    {
        var response = await _custodiaMasterService.ObterCustodiaMasterAsync();
        return Ok(response);
    }
}
