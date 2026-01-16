using FluentValidation;

namespace WageringStatsApi.Models;

public class WageringFeedConfigValidator : AbstractValidator<WageringFeedConfig>
{
    public WageringFeedConfigValidator()
    {
        RuleFor(x => x.CandidateId)
            .NotEmpty();

        RuleFor(x => x.WebSocketUrl)
            .NotEmpty()
            .Must(BeValidWebSocketUrl)
            .WithMessage("'{PropertyName}' must be a valid ws:// or wss:// URL");

        RuleFor(x => x.CustomerApiUrl)
            .NotEmpty()
            .Must(BeValidHttpUrl)
            .WithMessage("'{PropertyName}' must be a valid http:// or https:// URL");

        RuleFor(x => x.MaxFeedDurationMinutes)
            .GreaterThanOrEqualTo(3);

        RuleFor(x => x.MaxRetryAttempts)
            .GreaterThanOrEqualTo(0);
    }

    private static bool BeValidWebSocketUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "ws" or "wss";

    private static bool BeValidHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}
