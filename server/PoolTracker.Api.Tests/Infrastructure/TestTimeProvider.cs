namespace PoolTracker.Api.Tests.Infrastructure;

/// <summary>
/// A <see cref="TimeProvider"/> for tests. Flows real time by default so existing tests are
/// unaffected, but can be frozen to a fixed instant to make pool-day boundary logic deterministic.
/// </summary>
public sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset? frozen;

    public void Freeze(DateTimeOffset instant) => frozen = instant;

    public void Unfreeze() => frozen = null;

    public override DateTimeOffset GetUtcNow() => frozen ?? DateTimeOffset.UtcNow;
}
