using AutoRebalCarteiraAPI.Services;

namespace AutoRebalCarteiraAPI.Interfaces;

public interface IRebalanceamentoChannel
{
    ValueTask EnfileirarAsync(RebalanceamentoCestaCommand command);
}
