using System.Text.Json;
using Licenses.Application.LeaveManagement;
using Licenses.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Licenses.Infrastructure.Tests;

public sealed class OutboxPersistenceTests
{
    private static readonly DateTime Now = new(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task FreshMigrationCreatesOutboxTable()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            Assert.Equal(0, await db.OutboxMessages.CountAsync());
        });
    }

    [Fact]
    public async Task OutboxMessageCommitsAtomicallyWithBusinessTransaction()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var writer = new EfApplicationEventOutbox(db, new FixedTimeProvider(Now));
            var requestId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var orgUnitId = Guid.NewGuid();
            var operationId = Guid.NewGuid();

            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                await writer.EnqueueAsync(new LeaveRequestSubmitted(requestId, userId, orgUnitId, userId, Now), operationId, CancellationToken.None);
                await db.SaveChangesAsync();
            }

            db.ChangeTracker.Clear();
            Assert.Equal(0, await db.OutboxMessages.CountAsync());

            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                await writer.EnqueueAsync(new LeaveRequestSubmitted(requestId, userId, orgUnitId, userId, Now), operationId, CancellationToken.None);
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
            }

            db.ChangeTracker.Clear();
            var message = await db.OutboxMessages.SingleAsync();
            Assert.Equal(nameof(LeaveRequestSubmitted), message.EventType);
            Assert.Equal(operationId, message.CorrelationId);
            Assert.Null(message.ProcessedAtUtc);
            Assert.Equal(0, message.Attempts);

            using var payload = JsonDocument.Parse(message.Payload);
            Assert.Equal(requestId, payload.RootElement.GetProperty("leaveRequestId").GetGuid());
            Assert.Equal(userId, payload.RootElement.GetProperty("subjectUserId").GetGuid());
            Assert.Equal(orgUnitId, payload.RootElement.GetProperty("orgUnitId").GetGuid());
        });
    }

    [Fact]
    public async Task EventTypeAndCorrelationIdPreventDuplicateOutboxMessages()
    {
        await PostgreSqlTestDatabase.WithFreshDatabaseAsync(async db =>
        {
            var writer = new EfApplicationEventOutbox(db, new FixedTimeProvider(Now));
            var correlationId = Guid.NewGuid();
            var ev = new LeaveRequestApproved(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);

            await writer.EnqueueAsync(ev, correlationId, CancellationToken.None);
            await db.SaveChangesAsync();
            await writer.EnqueueAsync(ev, correlationId, CancellationToken.None);
            await db.SaveChangesAsync();

            Assert.Equal(1, await db.OutboxMessages.CountAsync());
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("INSERT INTO licenses.outbox_messages (id, event_type, payload, correlation_id, occurred_at_utc, created_at_utc, attempts) VALUES ({0}, {1}, {2}::jsonb, {3}, {4}, {5}, {6})", Guid.NewGuid(), nameof(LeaveRequestApproved), "{}", correlationId, Now, Now, 0));
        });
    }

    private sealed class FixedTimeProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}

