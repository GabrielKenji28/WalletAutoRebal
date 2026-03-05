using AutoRebalCarteiraAPI.DTOs;

namespace AutoRebalCarteiraAPI.Interfaces;

public interface ICestaService
{
    Task<CadastrarCestaResponse> CadastrarOuAlterarAsync(CadastrarCestaRequest request);
    Task<CestaAtualResponse> ObterCestaAtualAsync();
    Task<HistoricoCestasResponse> ObterHistoricoAsync();
}
