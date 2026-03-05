namespace AutoRebalCarteiraAPI.Interfaces;

public interface IMotorRebalanceamentoService
{
    Task RebalancearPorMudancaCestaAsync(int cestaAntigaId, int cestaNovaId);
    Task RebalancearPorDesvioProporcaoAsync();
}
