using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace PoolTracker.Api.Data;

/// <summary>
/// Design-time factory used by the EF Core tools (e.g. <c>dotnet ef migrations add</c>).
/// It builds the context without starting the web host, so migrations can be created and
/// scripted without the app's runtime configuration (JWT keys, hosted services, data seeding).
/// </summary>
public sealed class PoolTrackerDbContextFactory : IDesignTimeDbContextFactory<PoolTrackerDbContext>
{
    public PoolTrackerDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // A valid connection string is not required to generate or script migrations — only the
        // Npgsql provider is, so the generated SQL targets Postgres (production).
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=pooltracker_dev;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<PoolTrackerDbContext>()
            .UseNpgsql(connectionString);

        return new PoolTrackerDbContext(optionsBuilder.Options);
    }
}
