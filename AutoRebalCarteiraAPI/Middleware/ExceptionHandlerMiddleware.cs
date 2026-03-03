using AutoRebalCarteira.Domain.Exceptions;
using AutoRebalCarteiraAPI.DTOs;
using System.Text.Json;

namespace AutoRebalCarteiraAPI.Middleware;

public class ExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlerMiddleware> _logger;

    public ExceptionHandlerMiddleware(RequestDelegate next, ILogger<ExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BusinessException ex)
        {
            _logger.LogWarning("Erro de negocio: {Codigo} - {Mensagem}", ex.Codigo, ex.Message);
            context.Response.StatusCode = ex.StatusCode;
            context.Response.ContentType = "application/json";

            var response = new ErrorResponse { Erro = ex.Message, Codigo = ex.Codigo };
            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await context.Response.WriteAsync(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro interno nao tratado");
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";

            var response = new ErrorResponse { Erro = "Erro interno do servidor.", Codigo = "ERRO_INTERNO" };
            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await context.Response.WriteAsync(json);
        }
    }
}
