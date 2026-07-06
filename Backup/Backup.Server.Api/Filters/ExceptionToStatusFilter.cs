using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.WebUtilities;

namespace Backup.Server.Api.Filters;

// Maps well-known exception types to HTTP status codes so services
// can throw rather than thread Result<T> through every layer. Controllers
// keep their own try/catch where they want custom payloads — this filter
// only fires for exceptions that escape the controller untouched.
public sealed class ExceptionToStatusFilter : IExceptionFilter
{
    private readonly ILogger<ExceptionToStatusFilter> _logger;

    public ExceptionToStatusFilter(ILogger<ExceptionToStatusFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.ExceptionHandled)
        {
            return;
        }

        // Client disconnects / cancellation must not become 4xx/5xx responses.
        if (context.Exception is OperationCanceledException)
        {
            return;
        }

        var (status, message) = context.Exception switch
        {
            KeyNotFoundException ex => (StatusCodes.Status404NotFound, ex.Message),
            UnauthorizedAccessException ex => (StatusCodes.Status403Forbidden, ex.Message),
            InvalidOperationException ex => (StatusCodes.Status409Conflict, ex.Message),
            ApplicationException ex when ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
                => (StatusCodes.Status404NotFound, ex.Message),
            _ => (0, string.Empty)
        };

        if (status == 0)
        {
            return;
        }

        _logger.LogInformation(
            "Mapping {ExceptionType} to {Status}: {Message}",
            context.Exception.GetType().Name,
            status,
            message);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = ReasonPhrases.GetReasonPhrase(status),
            Detail = message
        };

        context.Result = new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
        context.ExceptionHandled = true;
    }
}
