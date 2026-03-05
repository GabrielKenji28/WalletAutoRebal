using AutoRebalCarteiraAPI.DTOs;

namespace AutoRebalCarteiraAPI.Interfaces;

public interface IMotorCompraService
{
    Task<ExecutarCompraResponse> ExecutarCompraAsync(DateOnly dataReferencia);
}
