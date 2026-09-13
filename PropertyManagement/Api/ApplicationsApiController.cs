using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Domain.Constants;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Application.Services;
using PropertyManagement.ViewModels.Api;

namespace PropertyManagement.Api;

/// <summary>
/// Provides the rental application list as paged, sorted, database-filtered JSON rows,
/// scoped by role: applicants see only their own applications, property managers see all.
/// </summary>
[ApiController]
[Route("api/applications")]
[Authorize]
public class ApplicationsApiController(IApplicationService applicationService, UserManager<User> userManager) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(userManager.GetUserId(User)!);

    /// <summary>
    /// Returns one page of rental applications matching the given filters, plus the total
    /// row count across all pages (independent of pageSize).
    /// </summary>
    /// <param name="status">Optional application status filter.</param>
    /// <param name="propertyId">Optional property filter.</param>
    /// <param name="sortBy">One of: createdAt (default), status, property, unit, applicant.</param>
    /// <param name="sortDir">"asc" or "desc" (default).</param>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Rows per page, clamped to 1-100 (default 10).</param>
    [HttpGet]
    [ProducesResponseType<ApplicationRowsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApplicationRowsResponse>> Get(
        [FromQuery] ApplicationStatus? status,
        [FromQuery] Guid? propertyId,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var applicantUserId = User.IsInRole(Roles.PropertyManager) ? (Guid?)null : CurrentUserId;
        var sortDescending = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);

        var result = await applicationService.GetFilteredApplicationsPageAsync(applicantUserId, status, propertyId, sortBy, sortDescending, page, pageSize);

        var response = new ApplicationRowsResponse
        {
            TotalCount = result.TotalCount,
            Rows = result.Rows.Select(a => new ApplicationRowDto
            {
                Id = a.Id,
                PropertyName = a.Unit.Property.Name,
                UnitNumber = a.Unit.UnitNumber,
                ApplicantFullName = a.FullName,
                ApplicantEmail = a.ApplicantUser.Email ?? string.Empty,
                Status = a.Status.ToString(),
                CreatedAt = a.CreatedAt
            }).ToList()
        };

        return Ok(response);
    }
}
