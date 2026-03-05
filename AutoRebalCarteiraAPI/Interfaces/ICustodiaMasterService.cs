using AutoRebalCarteiraAPI.DTOs;

namespace AutoRebalCarteiraAPI.Interfaces;

public interface ICustodiaMasterService
{
    Task<CustodiaMasterResponse> ObterCustodiaMasterAsync();
}
