using Licenses.Domain.LeaveManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Licenses.Infrastructure.Persistence.Configurations;

public sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("leave_requests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.OrgUnitId).HasColumnName("org_unit_id").IsRequired();
        builder.Property(x => x.LeaveTypeId).HasColumnName("leave_type_id").IsRequired();
        builder.Property(x => x.LeavePolicyVersionId).HasColumnName("leave_policy_version_id");
        builder.Property(x => x.StartDate).HasColumnName("start_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.EndDate).HasColumnName("end_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.DayPortion).HasColumnName("day_portion").HasMaxLength(16).HasConversion(x => ToDayPortion(x), x => FromDayPortion(x)).IsRequired();
        builder.Property(x => x.CalculatedDays).HasColumnName("calculated_days").HasPrecision(8, 2);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).HasConversion(x => ToStatus(x), x => FromStatus(x)).IsRequired();
        builder.Property(x => x.Comment).HasColumnName("comment").HasMaxLength(LeaveRequest.CommentMaxLength);
        builder.Property(x => x.BalanceAccountId).HasColumnName("balance_account_id");
        builder.Property(x => x.BalanceReservationOperationId).HasColumnName("balance_reservation_operation_id");
        builder.Property(x => x.SubmissionOperationId).HasColumnName("submission_operation_id");
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.Property(x => x.SubmittedAtUtc).HasColumnName("submitted_at_utc");
        builder.Property(x => x.DecidedAtUtc).HasColumnName("decided_at_utc");
        builder.Property(x => x.CancellationRequestedAtUtc).HasColumnName("cancellation_requested_at_utc");
        builder.Property(x => x.CancellationDecidedAtUtc).HasColumnName("cancellation_decided_at_utc");
        builder.Property(x => x.RevokedAtUtc).HasColumnName("revoked_at_utc");
        builder.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc");
        builder.HasOne<Licenses.Domain.Identity.User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Licenses.Domain.Identity.User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Licenses.Domain.Organization.OrgUnit>().WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LeaveType>().WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LeavePolicyVersion>().WithMany().HasForeignKey(x => x.LeavePolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BalanceAccount>().WithMany().HasForeignKey(x => x.BalanceAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.UserId, x.StartDate, x.EndDate, x.Status });
        builder.HasIndex(x => new { x.OrgUnitId, x.Status });
        builder.HasIndex(x => new { x.Status, x.EndDate, x.Id });
        builder.HasIndex(x => x.LeaveTypeId);
        builder.HasIndex(x => x.LeavePolicyVersionId);
        builder.HasIndex(x => x.BalanceReservationOperationId).IsUnique().HasFilter("balance_reservation_operation_id IS NOT NULL");
        builder.HasIndex(x => x.SubmissionOperationId).IsUnique().HasFilter("submission_operation_id IS NOT NULL");
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_leave_requests_status", "status IN ('DRAFT','PENDING_APPROVAL','APPROVED','REJECTED','CANCELLATION_REQUESTED','CANCELLED','REVOKED','COMPLETED')");
            t.HasCheckConstraint("ck_leave_requests_day_portion", "day_portion IN ('FULL_DAY','HALF_DAY')");
            t.HasCheckConstraint("ck_leave_requests_date_range", "end_date >= start_date");
            t.HasCheckConstraint("ck_leave_requests_half_day_single_date", "day_portion <> 'HALF_DAY' OR start_date = end_date");
            t.HasCheckConstraint("ck_leave_requests_calculated_positive", "calculated_days IS NULL OR calculated_days > 0");
            t.HasCheckConstraint("ck_leave_requests_submission_consistency", "(status = 'DRAFT' AND leave_policy_version_id IS NULL AND calculated_days IS NULL AND submitted_at_utc IS NULL AND balance_account_id IS NULL) OR (status <> 'DRAFT' AND leave_policy_version_id IS NOT NULL AND calculated_days IS NOT NULL AND submitted_at_utc IS NOT NULL)");
            t.HasCheckConstraint("ck_leave_requests_balance_link_consistency", "(balance_account_id IS NULL AND balance_reservation_operation_id IS NULL) OR (balance_account_id IS NOT NULL AND balance_reservation_operation_id IS NOT NULL)");
        });
    }

    public static string ToDayPortion(LeaveRequestDayPortion value) => value switch { LeaveRequestDayPortion.FullDay => "FULL_DAY", LeaveRequestDayPortion.HalfDay => "HALF_DAY", _ => throw new ArgumentOutOfRangeException(nameof(value)) };
    public static LeaveRequestDayPortion FromDayPortion(string value) => value switch { "FULL_DAY" => LeaveRequestDayPortion.FullDay, "HALF_DAY" => LeaveRequestDayPortion.HalfDay, _ => throw new ArgumentOutOfRangeException(nameof(value)) };
    public static string ToStatus(LeaveRequestStatus value) => value switch { LeaveRequestStatus.Draft => "DRAFT", LeaveRequestStatus.PendingApproval => "PENDING_APPROVAL", LeaveRequestStatus.Approved => "APPROVED", LeaveRequestStatus.Rejected => "REJECTED", LeaveRequestStatus.CancellationRequested => "CANCELLATION_REQUESTED", LeaveRequestStatus.Cancelled => "CANCELLED", LeaveRequestStatus.Revoked => "REVOKED", LeaveRequestStatus.Completed => "COMPLETED", _ => throw new ArgumentOutOfRangeException(nameof(value)) };
    public static LeaveRequestStatus FromStatus(string value) => value switch { "DRAFT" => LeaveRequestStatus.Draft, "PENDING_APPROVAL" => LeaveRequestStatus.PendingApproval, "APPROVED" => LeaveRequestStatus.Approved, "REJECTED" => LeaveRequestStatus.Rejected, "CANCELLATION_REQUESTED" => LeaveRequestStatus.CancellationRequested, "CANCELLED" => LeaveRequestStatus.Cancelled, "REVOKED" => LeaveRequestStatus.Revoked, "COMPLETED" => LeaveRequestStatus.Completed, _ => throw new ArgumentOutOfRangeException(nameof(value)) };
}

public sealed class LeaveRequestDecisionConfiguration : IEntityTypeConfiguration<LeaveRequestDecision>
{
    public void Configure(EntityTypeBuilder<LeaveRequestDecision> builder)
    {
        builder.ToTable("leave_request_decisions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.LeaveRequestId).HasColumnName("leave_request_id").IsRequired();
        builder.Property(x => x.Decision).HasColumnName("decision").HasMaxLength(16).HasConversion(x => ToDecision(x), x => FromDecision(x)).IsRequired();
        builder.Property(x => x.DecidedByUserId).HasColumnName("decided_by_user_id").IsRequired();
        builder.Property(x => x.Comment).HasColumnName("comment").HasMaxLength(LeaveRequestDecision.CommentMaxLength);
        builder.Property(x => x.OperationId).HasColumnName("operation_id").IsRequired();
        builder.Property(x => x.BalanceSettlementOperationId).HasColumnName("balance_settlement_operation_id");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.HasOne<LeaveRequest>().WithMany().HasForeignKey(x => x.LeaveRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Licenses.Domain.Identity.User>().WithMany().HasForeignKey(x => x.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.LeaveRequestId).IsUnique();
        builder.HasIndex(x => x.OperationId).IsUnique();
        builder.HasIndex(x => x.BalanceSettlementOperationId).IsUnique().HasFilter("balance_settlement_operation_id IS NOT NULL");
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_leave_request_decisions_decision", "decision IN ('APPROVE','REJECT')");
            t.HasCheckConstraint("ck_leave_request_decisions_reject_comment", "decision <> 'REJECT' OR (comment IS NOT NULL AND length(btrim(comment)) > 0)");
        });
    }

    public static string ToDecision(LeaveRequestDecisionKind value) => value switch { LeaveRequestDecisionKind.Approve => "APPROVE", LeaveRequestDecisionKind.Reject => "REJECT", _ => throw new ArgumentOutOfRangeException(nameof(value)) };
    public static LeaveRequestDecisionKind FromDecision(string value) => value switch { "APPROVE" => LeaveRequestDecisionKind.Approve, "REJECT" => LeaveRequestDecisionKind.Reject, _ => throw new ArgumentOutOfRangeException(nameof(value)) };
}



public sealed class LeaveRequestCancellationConfiguration : IEntityTypeConfiguration<LeaveRequestCancellation>
{
    public void Configure(EntityTypeBuilder<LeaveRequestCancellation> builder)
    {
        builder.ToTable("leave_request_cancellations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.LeaveRequestId).HasColumnName("leave_request_id").IsRequired();
        builder.Property(x => x.RequestedByUserId).HasColumnName("requested_by_user_id").IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(LeaveRequestCancellation.ReasonMaxLength).IsRequired();
        builder.Property(x => x.OperationId).HasColumnName("operation_id").IsRequired();
        builder.Property(x => x.RequestedAtUtc).HasColumnName("requested_at_utc").IsRequired();
        builder.Property(x => x.Decision).HasColumnName("decision").HasMaxLength(16).HasConversion(x => x == null ? null : ToCancellationDecision(x.Value), x => x == null ? null : FromCancellationDecision(x));
        builder.Property(x => x.DecidedByUserId).HasColumnName("decided_by_user_id");
        builder.Property(x => x.DecisionComment).HasColumnName("decision_comment").HasMaxLength(LeaveRequestCancellation.CommentMaxLength);
        builder.Property(x => x.DecisionOperationId).HasColumnName("decision_operation_id");
        builder.Property(x => x.BalanceSettlementOperationId).HasColumnName("balance_settlement_operation_id");
        builder.Property(x => x.DecidedAtUtc).HasColumnName("decided_at_utc");
        builder.HasOne<LeaveRequest>().WithMany().HasForeignKey(x => x.LeaveRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Licenses.Domain.Identity.User>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Licenses.Domain.Identity.User>().WithMany().HasForeignKey(x => x.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.LeaveRequestId).IsUnique();
        builder.HasIndex(x => x.OperationId).IsUnique();
        builder.HasIndex(x => x.DecisionOperationId).IsUnique().HasFilter("decision_operation_id IS NOT NULL");
        builder.HasIndex(x => x.BalanceSettlementOperationId).IsUnique().HasFilter("balance_settlement_operation_id IS NOT NULL");
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_leave_request_cancellations_decision", "decision IS NULL OR decision IN ('APPROVE','REJECT')");
            t.HasCheckConstraint("ck_leave_request_cancellations_reason", "length(btrim(reason)) > 0");
            t.HasCheckConstraint("ck_leave_request_cancellations_reject_comment", "decision <> 'REJECT' OR (decision_comment IS NOT NULL AND length(btrim(decision_comment)) > 0)");
            t.HasCheckConstraint("ck_leave_request_cancellations_decision_consistency", "(decision IS NULL AND decided_by_user_id IS NULL AND decision_operation_id IS NULL AND decided_at_utc IS NULL) OR (decision IS NOT NULL AND decided_by_user_id IS NOT NULL AND decision_operation_id IS NOT NULL AND decided_at_utc IS NOT NULL)");
        });
    }

    public static string ToCancellationDecision(LeaveRequestCancellationDecision value) => value switch { LeaveRequestCancellationDecision.Approve => "APPROVE", LeaveRequestCancellationDecision.Reject => "REJECT", _ => throw new ArgumentOutOfRangeException(nameof(value)) };
    public static LeaveRequestCancellationDecision FromCancellationDecision(string value) => value switch { "APPROVE" => LeaveRequestCancellationDecision.Approve, "REJECT" => LeaveRequestCancellationDecision.Reject, _ => throw new ArgumentOutOfRangeException(nameof(value)) };
}

public sealed class LeaveRequestRevocationConfiguration : IEntityTypeConfiguration<LeaveRequestRevocation>
{
    public void Configure(EntityTypeBuilder<LeaveRequestRevocation> builder)
    {
        builder.ToTable("leave_request_revocations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.LeaveRequestId).HasColumnName("leave_request_id").IsRequired();
        builder.Property(x => x.RevokedByUserId).HasColumnName("revoked_by_user_id").IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(LeaveRequestRevocation.ReasonMaxLength).IsRequired();
        builder.Property(x => x.OperationId).HasColumnName("operation_id").IsRequired();
        builder.Property(x => x.BalanceSettlementOperationId).HasColumnName("balance_settlement_operation_id");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.HasOne<LeaveRequest>().WithMany().HasForeignKey(x => x.LeaveRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Licenses.Domain.Identity.User>().WithMany().HasForeignKey(x => x.RevokedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.LeaveRequestId).IsUnique();
        builder.HasIndex(x => x.OperationId).IsUnique();
        builder.HasIndex(x => x.BalanceSettlementOperationId).IsUnique().HasFilter("balance_settlement_operation_id IS NOT NULL");
        builder.ToTable(t => t.HasCheckConstraint("ck_leave_request_revocations_reason", "length(btrim(reason)) > 0"));
    }
}
