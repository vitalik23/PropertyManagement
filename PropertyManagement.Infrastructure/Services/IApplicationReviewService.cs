namespace PropertyManagement.Infrastructure.Services;

public enum ReviewOutcome
{
    Approve,
    Return,
    Deny
}

public interface IApplicationReviewService
{
    Task<ApplicationActionResult> ReviewAsync(Guid applicationId, ReviewOutcome outcome, string? comment, Guid reviewerId);
}
