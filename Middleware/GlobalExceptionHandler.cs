using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        logger.LogError(exception, "Exceção de pedido não tratada");

        var (status, errorCode, message) = exception switch
        {
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "CONCURRENCY_CONFLICT",
                "O registo foi modificado por outro utilizador. Recarregue e tente novamente."
            ),
            DbUpdateException
                when exception.GetBaseException() is SqlException { Number: 2601 or 2627 } => (
                StatusCodes.Status409Conflict,
                "DUPLICATE_TAX_NUMBER",
                "Já existe um parceiro com este NIF."
            ),
            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                "NOT_FOUND",
                "O recurso solicitado não foi encontrado."
            ),
            ArgumentException or FormatException => (
                StatusCodes.Status400BadRequest,
                "INVALID_ARGUMENT",
                "Um ou mais valores fornecidos são inválidos."
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                "INTERNAL_SERVER_ERROR",
                "Ocorreu um erro interno."
            ),
        };

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            new
            {
                Status = status,
                ErrorCode = errorCode,
                Message = message,
            },
            cancellationToken
        );
        return true;
    }
}
