using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PoolTracker.Api.Data;
using PoolTracker.Api.Services;

namespace PoolTracker.Api.Tests.Infrastructure;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private SqliteConnection? sqliteConnection;

    public TestTimeProvider Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<PoolTrackerDbContext>>();
            services.RemoveAll<PoolTrackerDbContext>();

            sqliteConnection ??= new SqliteConnection("Data Source=:memory:");
            if (sqliteConnection.State != System.Data.ConnectionState.Open)
            {
                sqliteConnection.Open();
            }

            services.AddSingleton(sqliteConnection);
            services.AddDbContext<PoolTrackerDbContext>((provider, options) =>
            {
                options.UseSqlite(provider.GetRequiredService<SqliteConnection>());
            });

            // Drive the pool-day clock/engine from a controllable time source for deterministic tests.
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);

            // The pool-day sweep runs on a timer in production; tests drive the engine explicitly,
            // so remove the background service to keep database state deterministic.
            var sweepDescriptor = services.SingleOrDefault(
                descriptor => descriptor.ImplementationType == typeof(PoolDayBackgroundService));
            if (sweepDescriptor is not null)
            {
                services.Remove(sweepDescriptor);
            }
        });
    }

    public async Task InitializeAsync()
    {
        await ResetDatabaseAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        Clock.Unfreeze();

        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PoolTrackerDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
        await scope.ServiceProvider.SeedAsync(CancellationToken.None);
    }

    public async Task<T> ExecuteDbContextAsync<T>(Func<PoolTrackerDbContext, Task<T>> operation)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PoolTrackerDbContext>();
        return await operation(dbContext);
    }

    public async Task ExecuteDbContextAsync(Func<PoolTrackerDbContext, Task> operation)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PoolTrackerDbContext>();
        await operation(dbContext);
    }

    public async Task<T> ExecuteScopedAsync<T>(Func<IServiceProvider, Task<T>> operation)
    {
        await using var scope = Services.CreateAsyncScope();
        return await operation(scope.ServiceProvider);
    }

    public async Task ExecuteScopedAsync(Func<IServiceProvider, Task> operation)
    {
        await using var scope = Services.CreateAsyncScope();
        await operation(scope.ServiceProvider);
    }

    public HttpClient CreateApiClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (sqliteConnection is not null)
        {
            await sqliteConnection.DisposeAsync();
        }

        Dispose();
    }
}
