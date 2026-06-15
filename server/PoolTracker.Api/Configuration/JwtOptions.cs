namespace PoolTracker.Api.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "PoolTracker.Api";

    public string Audience { get; init; } = "PoolTracker.Client";

    public string SigningKey { get; init; } = "replace-this-with-a-minimum-32-character-signing-key";

    public int AccessTokenMinutes { get; init; } = 30;

    public int RefreshTokenDays { get; init; } = 14;
}
