namespace PoolTracker.Api.Tests.Infrastructure;

[Collection(ApiTestCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
    }

    protected CustomWebApplicationFactory Factory { get; }

    public virtual Task InitializeAsync()
    {
        return Factory.ResetDatabaseAsync();
    }

    public virtual Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    protected Task<TestAuthSession> RegisterAndLoginAsync(string displayNamePrefix = "Player")
    {
        return TestApi.RegisterAndLoginAsync(Factory, displayNamePrefix);
    }
}
