using Licenses.Application.Authorization;
using Licenses.Application.LeaveManagement;
using Licenses.Application.Notifications;
using Licenses.Domain.Authorization;
using Licenses.Domain.Identity;
using Licenses.Domain.LeaveManagement;
using Licenses.Domain.Notifications;
using Licenses.Domain.Organization;
using Licenses.Infrastructure.Notifications;
using Licenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Licenses.Infrastructure.Tests;

public sealed class OutboxWorkerTests
{
    private static readonly DateTime Now = new(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SuccessfulDeliveryMarksProcessedAndDoesNotProcessAgain()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var request = await SeedLeaveRequestAsync(db);
            var sender = new RecordingSender();
            await AddOutboxAsync(db, new LeaveRequestApproved(request.Id, request.UserId, request.OrgUnitId, Guid.NewGuid(), Now));
            var processor = CreateProcessor(db, sender);

            Assert.Equal(1, await processor.ProcessBatchAsync(CancellationToken.None));
            Assert.Equal(0, await processor.ProcessBatchAsync(CancellationToken.None));

            var message = await db.OutboxMessages.SingleAsync();
            Assert.NotNull(message.ProcessedAtUtc);
            Assert.Single(sender.Sent);
        });
    }

    [Theory]
    [InlineData(nameof(LeaveRequestApproved))]
    [InlineData(nameof(LeaveRequestRejected))]
    [InlineData(nameof(LeaveCancellationApproved))]
    [InlineData(nameof(LeaveCancellationRejected))]
    [InlineData(nameof(LeaveRequestRevoked))]
    public async Task CompletedLifecycleEventsNotifySubjectEmployee(string eventType)
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var request = await SeedLeaveRequestAsync(db);
            await AddOutboxAsync(db, CreateLifecycleEvent(eventType, request));
            var sender = new RecordingSender();

            await CreateProcessor(db, sender).ProcessBatchAsync(CancellationToken.None);

            Assert.Equal(["EMPLOYEE@EXAMPLE.COM"], sender.Sent.Select(x => x.RecipientEmail).ToArray());
        });
    }

    [Fact]
    public async Task SenderFailureDelaysRetryThenCanSucceed()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var clock = new MutableTimeProvider(Now);
            var request = await SeedLeaveRequestAsync(db);
            var sender = new RecordingSender { Fail = true };
            await AddOutboxAsync(db, new LeaveRequestApproved(request.Id, request.UserId, request.OrgUnitId, Guid.NewGuid(), Now));
            var processor = CreateProcessor(db, sender, clock);

            await processor.ProcessBatchAsync(CancellationToken.None);
            var failed = await db.OutboxMessages.SingleAsync();
            Assert.Equal(1, failed.Attempts);
            Assert.NotNull(failed.NextAttemptAtUtc);
            Assert.Null(failed.ProcessedAtUtc);

            sender.Fail = false;
            Assert.Equal(0, await processor.ProcessBatchAsync(CancellationToken.None));

            clock.Advance(TimeSpan.FromSeconds(11));
            db.ChangeTracker.Clear();
            Assert.Equal(1, await processor.ProcessBatchAsync(CancellationToken.None));
            Assert.NotNull((await db.OutboxMessages.SingleAsync()).ProcessedAtUtc);
        });
    }

    [Fact]
    public async Task MaximumAttemptsDeadLettersMessageAndBadMessageDoesNotBlockAnother()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var request = await SeedLeaveRequestAsync(db);
            await AddOutboxAsync(db, new LeaveRequestApproved(request.Id, request.UserId, request.OrgUnitId, Guid.NewGuid(), Now));
            await AddOutboxAsync(db, new LeaveRequestApproved(Guid.NewGuid(), request.UserId, request.OrgUnitId, Guid.NewGuid(), Now));
            var processor = CreateProcessor(db, new RecordingSender(), maxAttempts: 1);

            Assert.Equal(2, await processor.ProcessBatchAsync(CancellationToken.None));

            Assert.Equal(1, await db.OutboxMessages.CountAsync(x => x.ProcessedAtUtc != null));
            Assert.Equal(1, await db.OutboxMessages.CountAsync(x => x.DeadLetteredAtUtc != null));
        });
    }

    [Fact]
    public async Task ConcurrentProcessorsCannotClaimSameMessage()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async (db, connectionString) =>
        {
            var request = await SeedLeaveRequestAsync(db);
            await AddOutboxAsync(db, new LeaveRequestApproved(request.Id, request.UserId, request.OrgUnitId, Guid.NewGuid(), Now));

            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionString).Options;
            await using var db1 = new ApplicationDbContext(options);
            await using var db2 = new ApplicationDbContext(options);
            var sender = new RecordingSender(delay: TimeSpan.FromMilliseconds(100));
            var processor1 = CreateProcessor(db1, sender);
            var processor2 = CreateProcessor(db2, sender);

            await Task.WhenAll(
                processor1.ProcessBatchAsync(CancellationToken.None),
                processor2.ProcessBatchAsync(CancellationToken.None));

            Assert.Single(sender.Sent);
        });
    }

    [Fact]
    public async Task SubmittedRequestResolvesEligibleApproversOnly()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var request = await SeedLeaveRequestAsync(db);
            var approver = await SeedUserAsync(db, "Approver", "approver@example.com");
            var outOfScope = await SeedUserAsync(db, "Out", "out@example.com");
            var techAdmin = await SeedUserAsync(db, "Tech", "tech@example.com");
            await SeedPermissionRoleAsync(db, approver.Id, request.OrgUnitId, PermissionCodes.LeaveRequestsDecide);
            await SeedPermissionRoleAsync(db, outOfScope.Id, Guid.NewGuid(), PermissionCodes.LeaveRequestsDecide);
            await SeedPermissionRoleAsync(db, techAdmin.Id, request.OrgUnitId, "technical.admin");
            await AddOutboxAsync(db, new LeaveRequestSubmitted(request.Id, request.UserId, request.OrgUnitId, request.UserId, Now));
            var sender = new RecordingSender();

            await CreateProcessor(db, sender).ProcessBatchAsync(CancellationToken.None);

            Assert.Equal(["APPROVER@EXAMPLE.COM"], sender.Sent.Select(x => x.RecipientEmail).ToArray());
        });
    }

    [Fact]
    public async Task CancellationRequestResolvesCancellationDecisionActorsAndExcludesSubject()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var request = await SeedLeaveRequestAsync(db);
            var approver = await SeedUserAsync(db, "Cancel Approver", "cancel@example.com");
            await SeedPermissionRoleAsync(db, request.UserId, request.OrgUnitId, PermissionCodes.LeaveRequestsCancelDecide);
            await SeedPermissionRoleAsync(db, approver.Id, request.OrgUnitId, PermissionCodes.LeaveRequestsCancelDecide);
            await AddOutboxAsync(db, new LeaveCancellationRequested(request.Id, request.UserId, request.OrgUnitId, request.UserId, Now));
            var sender = new RecordingSender();

            await CreateProcessor(db, sender).ProcessBatchAsync(CancellationToken.None);

            Assert.Equal(["CANCEL@EXAMPLE.COM"], sender.Sent.Select(x => x.RecipientEmail).ToArray());
        });
    }

    [Fact]
    public async Task UnconfiguredSenderFailsClearly()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UnconfiguredNotificationSender().SendAsync(new("a@example.com", "A", "Subject", "Body", "id"), CancellationToken.None));

        Assert.Contains("not configured", ex.Message);
    }

    [Fact]
    public async Task DevelopmentSenderLogsAndDoesNotPretendToUseProvider()
    {
        var sender = new DevelopmentLoggingNotificationSender(NullLogger<DevelopmentLoggingNotificationSender>.Instance);

        await sender.SendAsync(new("a@example.com", "A", "Subject", "Body", "id"), CancellationToken.None);
    }

    private static OutboxMessageProcessor CreateProcessor(
        ApplicationDbContext db,
        INotificationSender sender,
        TimeProvider? clock = null,
        int maxAttempts = 5)
    {
        clock ??= new MutableTimeProvider(Now);
        var options = Options.Create(new OutboxProcessingOptions { BatchSize = 10, MaxAttempts = maxAttempts, InitialRetryDelay = TimeSpan.FromSeconds(10), MaxRetryDelay = TimeSpan.FromSeconds(30) });
        return new(db, new OutboxNotificationDeliveryPipeline(db, sender, clock), clock, options, NullLogger<OutboxMessageProcessor>.Instance);
    }

    private static async Task<LeaveRequest> SeedLeaveRequestAsync(ApplicationDbContext db)
    {
        var employee = await SeedUserAsync(db, "Employee", "employee@example.com");
        var orgUnit = OrgUnit.Create("People", "PEOPLE", null, Now);
        var leaveType = LeaveType.Create("VAC", "Vacation", null, 0, true, Now);
        db.OrgUnits.Add(orgUnit);
        db.LeaveTypes.Add(leaveType);
        var request = LeaveRequest.CreateDraft(employee.Id, orgUnit.Id, leaveType.Id, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2), LeaveRequestDayPortion.FullDay, null, employee.Id, Now);
        db.LeaveRequests.Add(request);
        await db.SaveChangesAsync();
        return request;
    }

    private static async Task<User> SeedUserAsync(ApplicationDbContext db, string name, string email)
    {
        var user = User.Create(name, email, null, Now);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task SeedPermissionRoleAsync(ApplicationDbContext db, Guid userId, Guid orgUnitId, string permissionCode)
    {
        if (!await db.OrgUnits.AnyAsync(x => x.Id == orgUnitId))
        {
            var other = OrgUnit.Create("Other", "OTHER" + Guid.NewGuid().ToString("N")[..8], null, Now);
            db.OrgUnits.Add(other);
            await db.SaveChangesAsync();
            orgUnitId = other.Id;
        }

        var normalizedPermissionCode = permissionCode.Trim().ToLowerInvariant();
        var permission = await db.Permissions.FirstOrDefaultAsync(x => x.Code == normalizedPermissionCode);
        if (permission is null)
        {
            permission = Permission.Create(permissionCode, permissionCode);
            db.Permissions.Add(permission);
        }

        var role = Role.Create("ROLE" + Guid.NewGuid().ToString("N")[..8], permissionCode, permissionCode, true, Now);
        db.Roles.Add(role);
        db.RolePermissions.Add(RolePermission.Create(role.Id, permission.Id));
        db.RoleScopeAssignments.Add(RoleScopeAssignment.Create(userId, role.Id, orgUnitId, includeDescendants: true, Now, null));
        await db.SaveChangesAsync();
    }

    private static async Task AddOutboxAsync(ApplicationDbContext db, IApplicationEvent applicationEvent)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(applicationEvent, applicationEvent.GetType(), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        db.OutboxMessages.Add(OutboxMessage.Create(applicationEvent.GetType().Name, payload, Guid.NewGuid(), applicationEvent.OccurredAtUtc, Now));
        await db.SaveChangesAsync();
    }

    private static IApplicationEvent CreateLifecycleEvent(string eventType, LeaveRequest request)
    {
        var actorId = Guid.NewGuid();
        return eventType switch
        {
            nameof(LeaveRequestApproved) => new LeaveRequestApproved(request.Id, request.UserId, request.OrgUnitId, actorId, Now),
            nameof(LeaveRequestRejected) => new LeaveRequestRejected(request.Id, request.UserId, request.OrgUnitId, actorId, Now),
            nameof(LeaveCancellationApproved) => new LeaveCancellationApproved(request.Id, request.UserId, request.OrgUnitId, actorId, Now),
            nameof(LeaveCancellationRejected) => new LeaveCancellationRejected(request.Id, request.UserId, request.OrgUnitId, actorId, Now),
            nameof(LeaveRequestRevoked) => new LeaveRequestRevoked(request.Id, request.UserId, request.OrgUnitId, actorId, Now),
            _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, "Unsupported test event type.")
        };
    }

    private sealed class RecordingSender(TimeSpan? delay = null) : INotificationSender
    {
        private readonly object gate = new();
        public bool Fail { get; set; }
        public List<NotificationMessage> Sent { get; } = [];

        public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken)
        {
            if (delay is { } value) await Task.Delay(value, cancellationToken);
            if (Fail) throw new InvalidOperationException("Sender failed.");
            lock (gate) Sent.Add(message);
        }
    }

    private sealed class MutableTimeProvider(DateTime now) : TimeProvider
    {
        private DateTime now = now;
        public override DateTimeOffset GetUtcNow() => new(now);
        public void Advance(TimeSpan value) => now = now.Add(value);
    }
}
