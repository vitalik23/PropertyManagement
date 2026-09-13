namespace PropertyManagement.Models.Api;

public class ApplicationRowDto
{
    public Guid Id { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;
    public string ApplicantFullName { get; set; } = string.Empty;
    public string ApplicantEmail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ApplicationRowsResponse
{
    public List<ApplicationRowDto> Rows { get; set; } = [];
    public int TotalCount { get; set; }
}
