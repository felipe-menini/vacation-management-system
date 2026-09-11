using System.Reflection;
using System.Text.Json;
using Licenses.Application.Audit;
using Licenses.Domain.Audit;
using Licenses.Domain.Identity;
using Licenses.Domain.LeaveManagement;
using Licenses.Domain.Organization;
using Licenses.Infrastructure.Audit;
using Licenses.Infrastructure.Organization;
using Licenses.Infrastructure.LeaveManagement;
using Licenses.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace Licenses.Infrastructure.Tests;

public sealed class AuditPersistenceTests
{
    [Fact]
    public async Task AuditEventPersistsSystemActorAndMetadataRoundTrips()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var (actor, subject, orgUnit) = await SeedAuditReferencesAsync(db);
            var writer = new EfAuditWriter(db);
            var resourceId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            const string metadata = """{"from":"PENDING_APPROVAL","to":"APPROVED","days":3.5}""";

            await writer.WriteAsync(new AuditEventData(
                actor.Id,
                "leave.request.approve",
                "LeaveRequest",
                resourceId,
                subject.Id,
                orgUnit.Id,
                correlationId,
                DateTime.UtcNow,
                metadata), CancellationToken.None);

            await writer.WriteAsync(new AuditEventData(
                null,
                "system.job.run",
                "NotificationOutbox",
                null,
                null,
                null,
                Guid.NewGuid(),
                DateTime.UtcNow,
                """{"result":"ok"}"""), CancellationToken.None);
            await db.SaveChangesAsync();

            db.ChangeTracker.Clear();
            var events = await db.AuditEvents.OrderBy(x => x.OccurredAtUtc).ToListAsync();

            Assert.Equal(2, events.Count);
            var businessEvent = Assert.Single(events, x => x.Action == "leave.request.approve");
            Assert.Equal(actor.Id, businessEvent.ActorUserId);
            Assert.Equal("LeaveRequest", businessEvent.ResourceType);
            Assert.Equal(resourceId, businessEvent.ResourceId);
            Assert.Equal(subject.Id, businessEvent.SubjectUserId);
            Assert.Equal(orgUnit.Id, businessEvent.OrgUnitId);
            Assert.Equal(correlationId, businessEvent.CorrelationId);
            using var document = JsonDocument.Parse(businessEvent.MetadataJson!);
            Assert.Equal("PENDING_APPROVAL", document.RootElement.GetProperty("from").GetString());
            Assert.Equal(3.5m, document.RootElement.GetProperty("days").GetDecimal());

            var systemEvent = Assert.Single(events, x => x.Action == "system.job.run");
            Assert.Null(systemEvent.ActorUserId);
        });
    }

    [Fact]
    public async Task AuditForeignKeysUseConservativeRestrictBehavior()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var entityType = db.Model.FindEntityType(typeof(AuditEvent));
            Assert.NotNull(entityType);
            var foreignKeys = entityType!.GetForeignKeys().ToDictionary(x => x.Properties.Single().Name);

            Assert.Equal(DeleteBehavior.Restrict, foreignKeys[nameof(AuditEvent.ActorUserId)].DeleteBehavior);
            Assert.Equal(DeleteBehavior.Restrict, foreignKeys[nameof(AuditEvent.SubjectUserId)].DeleteBehavior);
            Assert.Equal(DeleteBehavior.Restrict, foreignKeys[nameof(AuditEvent.OrgUnitId)].DeleteBehavior);
        });
    }

    [Fact]
    public async Task PostgreSqlRejectsDirectAuditUpdateAndDelete()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var writer = new EfAuditWriter(db);
            await writer.WriteAsync(new AuditEventData(null, "system.job.run", "NotificationOutbox", null, null, null, Guid.NewGuid(), DateTime.UtcNow, null), CancellationToken.None);
            await db.SaveChangesAsync();
            var auditEvent = await db.AuditEvents.SingleAsync();

            await Assert.ThrowsAsync<PostgresException>(() =>
                db.Database.ExecuteSqlInterpolatedAsync($"UPDATE licenses.audit_events SET action = {"system.job.retry"} WHERE id = {auditEvent.Id}"));

            await Assert.ThrowsAsync<PostgresException>(() =>
                db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM licenses.audit_events WHERE id = {auditEvent.Id}"));
        });
    }

    [Fact]
    public async Task AuditWriterParticipatesInExistingTransactionRollback()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var writer = new EfAuditWriter(db);
            await using var transaction = await db.Database.BeginTransactionAsync();
            await writer.WriteAsync(new AuditEventData(null, "system.job.run", "NotificationOutbox", null, null, null, Guid.NewGuid(), DateTime.UtcNow, null), CancellationToken.None);
            await db.SaveChangesAsync();

            await transaction.RollbackAsync();
            db.ChangeTracker.Clear();

            Assert.Equal(0, await db.AuditEvents.CountAsync());
        });
    }

    [Fact]
    public async Task AuditWriterParticipatesInExistingTransactionCommit()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var writer = new EfAuditWriter(db);
            await using var transaction = await db.Database.BeginTransactionAsync();
            await writer.WriteAsync(new AuditEventData(null, "system.job.run", "NotificationOutbox", null, null, null, Guid.NewGuid(), DateTime.UtcNow, null), CancellationToken.None);
            await db.SaveChangesAsync();

            await transaction.CommitAsync();
            db.ChangeTracker.Clear();

            Assert.Equal(1, await db.AuditEvents.CountAsync());
        });
    }

    [Fact]
    public async Task AdministrativeBalanceMutationAndAuditRollBackTogetherAndRetryDoesNotDuplicate()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var now = DateTime.UtcNow;
            var actor = User.Create("Balance Admin", $"balance.admin.{Guid.NewGuid():N}@example.test", null, now);
            var subject = User.Create("Balance Subject", $"balance.subject.{Guid.NewGuid():N}@example.test", null, now);
            var bucket = BalanceBucket.Create("VACATION_DAYS_" + Guid.NewGuid().ToString("N")[..8], "Vacation Days", null, BalanceBucketUnit.Day, true, now);
            await db.Users.AddRangeAsync(actor, subject);
            await db.BalanceBuckets.AddAsync(bucket);
            await db.SaveChangesAsync();

            var repo = new EfBalanceRepository(db, new EfAuditWriter(db));
            var operationId = Guid.NewGuid();
            await repo.MutateAsync(subject.Id, bucket.Id, operationId, BalanceLedgerEntryType.Grant, 8m, "Initial admin grant", actor.Id, now, new(actor.Id, "balance.grant"), CancellationToken.None);
            await repo.MutateAsync(subject.Id, bucket.Id, operationId, BalanceLedgerEntryType.Grant, 8m, "Initial admin grant", actor.Id, now, new(actor.Id, "balance.grant"), CancellationToken.None);

            Assert.Equal(1, await db.BalanceLedgerEntries.CountAsync());
            Assert.Equal(1, await db.AuditEvents.CountAsync(x => x.Action == "balance.grant"));

            await using var transaction = await db.Database.BeginTransactionAsync();
            await repo.MutateAsync(subject.Id, bucket.Id, Guid.NewGuid(), BalanceLedgerEntryType.Adjustment, 2m, "Rollback adjustment", actor.Id, now, new(actor.Id, "balance.adjust"), CancellationToken.None);
            await transaction.RollbackAsync();
            db.ChangeTracker.Clear();

            Assert.Equal(1, await db.BalanceLedgerEntries.CountAsync());
            Assert.Equal(1, await db.AuditEvents.CountAsync());
        });
    }

    [Fact]
    public async Task OrganizationMutationAndAuditRollBackTogether()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var now = DateTime.UtcNow;
            var actor = User.Create("Organization Admin", $"organization.admin.{Guid.NewGuid():N}@example.test", null, now);
            await db.Users.AddAsync(actor);
            await db.SaveChangesAsync();

            var service = new Licenses.Application.Organization.OrganizationService(new EfOrganizationRepository(db), TimeProvider.System, new EfAuditWriter(db));

            await using var transaction = await db.Database.BeginTransactionAsync();
            await service.CreateOrgUnitAsync(new("Rollback Unit", "ROLLBACK_" + Guid.NewGuid().ToString("N")[..8], null), CancellationToken.None, actor.Id);
            await transaction.RollbackAsync();
            db.ChangeTracker.Clear();

            Assert.Equal(0, await db.OrgUnits.CountAsync(x => x.Name == "Rollback Unit"));
            Assert.Equal(0, await db.AuditEvents.CountAsync(x => x.Action == "organization.unit.create"));
        });
    }

    [Fact]
    public async Task AuditEventReaderEnforcesScopeGlobalVisibilityFiltersAndDeterministicPaging()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var now = new DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc);
            var root = OrgUnit.Create("Company", "COMPANY_" + Guid.NewGuid().ToString("N")[..8], null, now);
            var child = OrgUnit.Create("Child", "CHILD_" + Guid.NewGuid().ToString("N")[..8], root.Id, now);
            var outside = OrgUnit.Create("Outside", "OUTSIDE_" + Guid.NewGuid().ToString("N")[..8], null, now);
            var actor = User.Create("Audit Reader Actor", $"audit.reader.actor.{Guid.NewGuid():N}@example.test", null, now);
            var subject = User.Create("Audit Reader Subject", $"audit.reader.subject.{Guid.NewGuid():N}@example.test", null, now);
            await db.OrgUnits.AddRangeAsync(root, child, outside);
            await db.Users.AddRangeAsync(actor, subject);
            await db.SaveChangesAsync();

            var writer = new EfAuditWriter(db);
            var childResourceId = Guid.NewGuid();
            await writer.WriteAsync(new(actor.Id, "leave.request.create", "LeaveRequest", childResourceId, subject.Id, child.Id, Guid.NewGuid(), now.AddMinutes(-3), """{"safe":true}"""), CancellationToken.None);
            await writer.WriteAsync(new(actor.Id, "leave.request.approve", "LeaveRequest", Guid.NewGuid(), subject.Id, outside.Id, Guid.NewGuid(), now.AddMinutes(-2), null), CancellationToken.None);
            await writer.WriteAsync(new(actor.Id, "system.job.run", "NotificationOutbox", null, null, null, Guid.NewGuid(), now.AddMinutes(-1), null), CancellationToken.None);
            await db.SaveChangesAsync();

            var reader = new EfAuditEventReader(db);
            var scopedResult = await reader.SearchAsync(new(null, null, null, null, null, null, null, null, 1, 50), new HashSet<Guid> { child.Id }, canReadGlobalEvents: false, CancellationToken.None);

            var scopedEvent = Assert.Single(scopedResult.Items);
            Assert.Equal("leave.request.create", scopedEvent.Action);
            Assert.Equal(actor.DisplayName, scopedEvent.ActorDisplayName);
            Assert.Equal(subject.DisplayName, scopedEvent.SubjectDisplayName);
            Assert.Equal(child.Name, scopedEvent.OrgUnitName);

            var filtered = await reader.SearchAsync(new(now.AddMinutes(-4), now, "leave.request.create", "LeaveRequest", childResourceId, actor.Id, subject.Id, child.Id, 1, 50), new HashSet<Guid> { child.Id }, canReadGlobalEvents: false, CancellationToken.None);
            Assert.Equal(scopedEvent.Id, Assert.Single(filtered.Items).Id);

            var globalResult = await reader.SearchAsync(new(null, null, null, null, null, null, null, null, 1, 50), new HashSet<Guid> { root.Id, child.Id }, canReadGlobalEvents: true, CancellationToken.None);
            Assert.Equal(["system.job.run", "leave.request.create"], globalResult.Items.Select(x => x.Action).ToArray());
        });
    }

    [Fact]
    public async Task AuditEventReaderCapsPageSizeAndReportsNextPage()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var now = new DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc);
            var orgUnit = OrgUnit.Create("Audit Page", "AUDIT_PAGE_" + Guid.NewGuid().ToString("N")[..8], null, now);
            await db.OrgUnits.AddAsync(orgUnit);
            await db.SaveChangesAsync();

            var writer = new EfAuditWriter(db);
            for (var i = 0; i < 105; i++)
            {
                await writer.WriteAsync(new(null, "audit.page", "AuditPage", Guid.NewGuid(), null, orgUnit.Id, null, now.AddSeconds(i), null), CancellationToken.None);
            }
            await db.SaveChangesAsync();

            var reader = new EfAuditEventReader(db);
            var result = await reader.SearchAsync(new(null, null, null, null, null, null, null, null, 1, 250), new HashSet<Guid> { orgUnit.Id }, canReadGlobalEvents: false, CancellationToken.None);

            Assert.Equal(100, result.PageSize);
            Assert.Equal(105, result.TotalCount);
            Assert.True(result.HasNextPage);
            Assert.Equal(100, result.Items.Count);
            Assert.True(result.Items.Zip(result.Items.Skip(1), (left, right) => left.OccurredAtUtc >= right.OccurredAtUtc).All(x => x));
        });
    }

    [Fact]
    public async Task MigrationChainCreatesAuditEventsTable()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var tableExists = await db.Database.SqlQueryRaw<bool>("""
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'licenses' AND table_name = 'audit_events') AS "Value"
                """).SingleAsync();

            Assert.True(tableExists);
        });
    }

    [Fact]
    public void AuditEventExposesNoNormalUpdateOrDeleteMethods()
    {
        var publicInstanceMethods = typeof(AuditEvent)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(x => x.Name)
            .ToArray();

        Assert.DoesNotContain(publicInstanceMethods, name => name.StartsWith("Update", StringComparison.Ordinal) || name.StartsWith("Delete", StringComparison.Ordinal) || name.StartsWith("Remove", StringComparison.Ordinal));
    }

    private static async Task<(User Actor, User Subject, OrgUnit OrgUnit)> SeedAuditReferencesAsync(ApplicationDbContext db)
    {
        var now = DateTime.UtcNow;
        var orgUnit = OrgUnit.Create("Audit Unit", "AUDIT_" + Guid.NewGuid().ToString("N")[..8], null, now);
        var actor = User.Create("Audit Actor", $"audit.actor.{Guid.NewGuid():N}@example.test", null, now);
        var subject = User.Create("Audit Subject", $"audit.subject.{Guid.NewGuid():N}@example.test", null, now);
        await db.OrgUnits.AddAsync(orgUnit);
        await db.Users.AddRangeAsync(actor, subject);
        await db.SaveChangesAsync();
        return (actor, subject, orgUnit);
    }
}
