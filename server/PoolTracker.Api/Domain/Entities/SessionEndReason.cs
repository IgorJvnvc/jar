namespace PoolTracker.Api.Domain.Entities;

public enum SessionEndReason
{
    /// <summary>The player explicitly ended the session and submitted a report.</summary>
    Manual = 0,

    /// <summary>The background engine stopped a session that stayed open past the idle cap.</summary>
    AutoIdle = 1,

    /// <summary>The background engine closed a session that was still open at the pool-day boundary.</summary>
    AutoDayClose = 2
}
