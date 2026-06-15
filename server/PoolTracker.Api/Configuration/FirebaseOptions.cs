namespace PoolTracker.Api.Configuration;

public sealed class FirebaseOptions
{
    public const string SectionName = "Firebase";

    public string? CredentialsPath { get; init; }

    public string? ProjectId { get; init; }
}
