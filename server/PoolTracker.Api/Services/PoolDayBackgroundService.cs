using Microsoft.Extensions.Options;
using PoolTracker.Api.Configuration;

namespace PoolTracker.Api.Services;

/// <summary>
/// Periodically runs the <see cref="IPoolDayEngine"/> to auto-stop stale sessions and finalize
/// closed hall competitions. Resolves the engine from a fresh scope each tick because the engine
/// (and its DbContext) are scoped services.
/// </summary>
public sealed class PoolDayBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly PoolDayOptions options;
    private readonly ILogger<PoolDayBackgroundService> logger;

    public PoolDayBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<PoolDayOptions> options,
        ILogger<PoolDayBackgroundService> logger)
    {
        this.scopeFactory = scopeFactory;
        this.options = options.Value;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(options.SweepIntervalMinutes, 1));

        // Catch up immediately on startup (e.g. sessions left open while the service was down).
        await RunOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown.
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var engine = scope.ServiceProvider.GetRequiredService<IPoolDayEngine>();
            await engine.RunAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down — ignore.
        }
        catch (Exception ex)
        {
            // Never let a sweep failure crash the host; log and try again next tick.
            logger.LogError(ex, "Pool-day sweep failed.");
        }
    }
}
