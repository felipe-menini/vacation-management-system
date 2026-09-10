using System.Net;
using System.Net.Http.Json;
using Licenses.Application.Authorization;
using Licenses.Application.LeaveManagement;
using Licenses.Domain.Authorization;
using Licenses.Domain.Identity;
using Licenses.Domain.LeaveManagement;
using Licenses.Domain.Organization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Licenses.Api.Tests;

public sealed class WorkingCalendarEndpointAuthorizationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task WorkingCalendarsGet_Returns401_WhenActorIsMissing()
    {
        using var client = CreateClientWithActor(null, []);
        using var response = await client.GetAsync("/api/working-calendars");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReadOnlyActorCanGetButCannotMutate()
    {
        using var client = CreateClientWithActor(Guid.NewGuid(), [PermissionCodes.LeaveCalendarsRead]);

        using var get = await client.GetAsync("/api/working-calendars");
        using var post = await client.PostAsJsonAsync("/api/working-calendars", CalendarPayload());

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
    }

    [Fact]
    public async Task HrManageActorCanMutateAndCalculate()
    {
        var repository = new FakeWorkingCalendarRepository();
        using var client = CreateClientWithActor(Guid.NewGuid(), [PermissionCodes.LeaveCalendarsRead, PermissionCodes.LeaveCalendarsManage], repository);

        using var post = await client.PostAsJsonAsync("/api/working-calendars", CalendarPayload());
        var calendar = await post.Content.ReadFromJsonAsync<WorkingCalendarDto>();
        using var calculate = await client.GetAsync($"/api/day-calculation?mode=BUSINESS_DAYS&workingCalendarId={calendar!.Id}&startDate=2026-08-10&endDate=2026-08-14");
        var result = await calculate.Content.ReadFromJsonAsync<DayCalculationDto>();

        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        Assert.Equal(HttpStatusCode.OK, calculate.StatusCode);
        Assert.Equal(5, result!.CalculatedDays);
    }

    [Fact]
    public async Task TechAdminWithoutBusinessPermissionCannotManageCalendars()
    {
        using var client = CreateClientWithActor(Guid.NewGuid(), [PermissionCodes.OrgUnitsRead], roleCode: "TECH_ADMIN");
        using var response = await client.PostAsJsonAsync("/api/working-calendars", CalendarPayload());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CalculationEndpointRequiresCalendarReadPermission()
    {
        using var client = CreateClientWithActor(Guid.NewGuid(), []);
        using var response = await client.GetAsync($"/api/day-calculation?mode=CALENDAR_DAYS&startDate=2026-08-10&endDate=2026-08-14");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient CreateClientWithActor(Guid? actorId, string[] permissionCodes, FakeWorkingCalendarRepository? repository = null, string roleCode = "TEST") =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.AddScoped<ICurrentActor>(_ => new FixedCurrentActor(actorId));
                services.AddScoped<IAuthorizationRepository>(_ => new FakeAuthorizationRepository(actorId, permissionCodes, roleCode));
                services.AddScoped<IWorkingCalendarRepository>(_ => repository ?? new FakeWorkingCalendarRepository());
            });
        }).CreateClient();

    private static object CalendarPayload() => new
    {
        code = "STANDARD",
        name = "Standard",
        description = "Test calendar",
        isActive = true,
        weekdays = Weekdays()
    };

    private static object[] Weekdays() =>
    [
        new { dayOfWeek = "Sunday", isWorkingDay = false },
        new { dayOfWeek = "Monday", isWorkingDay = true },
        new { dayOfWeek = "Tuesday", isWorkingDay = true },
        new { dayOfWeek = "Wednesday", isWorkingDay = true },
        new { dayOfWeek = "Thursday", isWorkingDay = true },
        new { dayOfWeek = "Friday", isWorkingDay = true },
        new { dayOfWeek = "Saturday", isWorkingDay = false }
    ];

    private sealed class FixedCurrentActor(Guid? userId) : ICurrentActor { public Guid? UserId => userId; }

    private sealed class FakeAuthorizationRepository : IAuthorizationRepository
    {
        private readonly Guid? _actorId;
        private readonly List<Permission> _permissions;
        private readonly Role _role;

        public FakeAuthorizationRepository(Guid? actorId, IEnumerable<string> permissionCodes, string roleCode)
        {
            _actorId = actorId;
            _role = Role.Create(roleCode, roleCode, "Test role", true, DateTime.UtcNow);
            _permissions = permissionCodes.Select(x => Permission.Create(x, x)).ToList();
        }

        public Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<User?>(User.Create("Actor", "actor.calendar@example.test", null, DateTime.UtcNow));
        public Task<OrgUnit?> GetOrgUnitAsync(Guid orgUnitId, CancellationToken cancellationToken) => Task.FromResult<OrgUnit?>(null);
        public Task<List<OrgUnit>> ListOrgUnitsAsync(CancellationToken cancellationToken) => Task.FromResult(new List<OrgUnit>());
        public Task<List<UserOrgAssignment>> ListActiveUserOrgAssignmentsAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(new List<UserOrgAssignment>());
        public Task<List<RoleScopeAssignment>> ListActiveRoleScopeAssignmentsAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(_actorId == userId ? [RoleScopeAssignment.Create(userId, _role.Id, Guid.NewGuid(), false, DateTime.UtcNow.AddDays(-1), null)] : new List<RoleScopeAssignment>());
        public Task<bool> RoleHasPermissionAsync(Guid roleId, string permissionCode, CancellationToken cancellationToken) => Task.FromResult(roleId == _role.Id && _permissions.Any(x => x.Code == permissionCode));
        public Task<bool> IsRoleActiveAsync(Guid roleId, CancellationToken cancellationToken) => Task.FromResult(roleId == _role.Id && _role.IsActive);
        public Task<List<User>> ListUsersInOrgUnitsAsync(IReadOnlyCollection<Guid> orgUnitIds, DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(new List<User>());
        public Task<List<DevelopmentActorDto>> ListDevelopmentActorsAsync(DateTime utcNow, CancellationToken cancellationToken) => Task.FromResult(new List<DevelopmentActorDto>());
    }

    private sealed class FakeWorkingCalendarRepository : IWorkingCalendarRepository
    {
        private readonly List<WorkingCalendar> _calendars = [];

        public Task<List<WorkingCalendar>> ListCalendarsAsync(CancellationToken cancellationToken) => Task.FromResult(_calendars.ToList());
        public Task<WorkingCalendar?> GetCalendarAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_calendars.SingleOrDefault(x => x.Id == id));
        public Task<WorkingCalendarException?> GetExceptionAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_calendars.SelectMany(x => x.Exceptions).SingleOrDefault(x => x.Id == id));
        public Task<bool> CodeExistsAsync(string normalizedCode, Guid? excludingCalendarId, CancellationToken cancellationToken) => Task.FromResult(_calendars.Any(x => x.Code == normalizedCode && (excludingCalendarId is null || x.Id != excludingCalendarId)));
        public Task<bool> ExceptionDateExistsAsync(Guid calendarId, DateOnly date, Guid? excludingExceptionId, CancellationToken cancellationToken) => Task.FromResult(_calendars.Single(x => x.Id == calendarId).Exceptions.Any(x => x.Date == date && (excludingExceptionId is null || x.Id != excludingExceptionId)));
        public Task AddCalendarAsync(WorkingCalendar calendar, CancellationToken cancellationToken) { _calendars.Add(calendar); return Task.CompletedTask; }
        public Task AddExceptionAsync(WorkingCalendarException exception, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
