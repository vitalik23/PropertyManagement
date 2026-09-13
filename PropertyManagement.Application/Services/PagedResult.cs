namespace PropertyManagement.Application.Services;

public record PagedResult<T>(List<T> Rows, int TotalCount);
