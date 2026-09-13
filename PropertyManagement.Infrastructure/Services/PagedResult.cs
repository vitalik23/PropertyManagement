namespace PropertyManagement.Infrastructure.Services;

public record PagedResult<T>(List<T> Rows, int TotalCount);
