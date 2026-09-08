namespace OFC.PrintAgent;

// The agent authenticates as any other OFC client: an operator signs in once (POST /api/v1/auth/login,
// same as the web app) and pastes the resulting session token here. There is no separate "agent" auth
// flow in this codebase, so it reuses the existing bearer-session model rather than inventing a new one.
public sealed class PrintAgentOptions
{
    public const string SectionName = "PrintAgent";
    public required string ApiBaseUrl { get; init; }
    public required string SessionToken { get; init; }
    public required Guid BranchId { get; init; }
    public int PollIntervalSeconds { get; init; } = 3;
}
