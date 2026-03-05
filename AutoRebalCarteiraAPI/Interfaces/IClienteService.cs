using AutoRebalCarteiraAPI.DTOs;

namespace AutoRebalCarteiraAPI.Interfaces;

public interface IClienteService
{
    Task<AdesaoResponse> AderirAsync(AdesaoRequest request);
    Task<SaidaResponse> SairAsync(int clienteId);
    Task<AlterarValorMensalResponse> AlterarValorMensalAsync(int clienteId, AlterarValorMensalRequest request);
    Task<CarteiraResponse> ConsultarCarteiraAsync(int clienteId);
    Task<RentabilidadeResponse> ConsultarRentabilidadeAsync(int clienteId);
}
