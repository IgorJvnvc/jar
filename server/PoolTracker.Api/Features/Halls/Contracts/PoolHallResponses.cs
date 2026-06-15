namespace PoolTracker.Api.Features.Halls.Contracts;

public sealed record PoolHallResponse(
    Guid Id,
    string Name,
    string Address,
    double Latitude,
    double Longitude,
    int TotalTables,
    decimal OverallScore,
    int RatingsCount,
    DateTimeOffset CreatedAtUtc
);

public sealed record PoolHallTableResponse(
    Guid Id,
    string TableLabel,
    decimal OverallScore,
    int RatingsCount
);

public sealed record PoolHallDetailResponse(
    Guid Id,
    string Name,
    string Address,
    double Latitude,
    double Longitude,
    int TotalTables,
    decimal OverallScore,
    int RatingsCount,
    IReadOnlyList<PoolHallTableResponse> Tables
);
