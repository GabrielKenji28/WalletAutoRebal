namespace AutoRebalCarteira.Domain.Exceptions;

public class BusinessException : Exception
{
    public string Codigo { get; }
    public int StatusCode { get; }

    public BusinessException(string message, string codigo, int statusCode = 400)
        : base(message)
    {
        Codigo = codigo;
        StatusCode = statusCode;
    }
}
