using CrmKanban.Domain.Common;
using CrmKanban.Domain.Enums;
using CrmKanban.Domain.Tickets;

namespace CrmKanban.Domain.Entities;

/// <summary>
/// A support ticket (spec §11, §12). Encapsulated: status only moves through the behavior
/// methods, which run the <see cref="TicketStateMachine"/> and stamp the reporting timestamps
/// (first response / resolved / closed) by status category — never by name (spec §4.3).
/// </summary>
public sealed class Ticket : Entity
{
    private Ticket() { } // EF

    public Ticket(
        Guid companyId,
        string number,
        Guid openedById,
        Guid initialStatusId,
        string title,
        string body,
        Priority priority = Priority.Normal,
        Guid? categoryId = null)
    {
        CompanyId = companyId;
        Number = number;
        OpenedById = openedById;
        StatusId = initialStatusId;
        Title = title.Trim();
        Body = body;
        Priority = priority;
        CategoryId = categoryId;
    }

    public string Number { get; private set; } = null!;
    public Guid CompanyId { get; private set; }
    public Guid OpenedById { get; private set; }
    public Guid? AssignedToId { get; private set; }
    public Guid StatusId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public Priority Priority { get; private set; }
    public string Title { get; private set; } = null!;
    public string Body { get; private set; } = null!;

    public DateTime? FirstResponseAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    /// <summary>Values captured from the company's configurable form fields (spec §4.6), stored as a
    /// denormalized JSON array of {label, value} so it stays readable even if a field is later renamed or
    /// removed. Null when the form had no extra fields.</summary>
    public string? CustomFieldsJson { get; private set; }

    public void SetCustomFields(string? json) => CustomFieldsJson = json;

    /// <summary>
    /// What this opportunity is expected to be worth, in the company's configured currency
    /// (Faz 39). Null = nobody has priced it yet, which is different from zero — an unpriced
    /// opportunity is excluded from forecasts rather than counted as worthless.
    /// </summary>
    public decimal? EstimatedValue { get; private set; }

    /// <summary>
    /// What it actually came to. Kept apart from <see cref="EstimatedValue"/> on purpose: with a
    /// single field the estimate is overwritten on close and "how accurate are my forecasts?" becomes
    /// unanswerable. Null on a won ticket means the estimate stood — reporting falls back to it, so
    /// nobody has to type the same number twice.
    /// </summary>
    public decimal? ActualValue { get; private set; }

    /// <summary>The figure reporting should count for this ticket: the realised amount once known,
    /// otherwise the estimate.</summary>
    public decimal? ReportableValue => ActualValue ?? EstimatedValue;

    /// <summary>
    /// Prices the opportunity. Negative amounts are rejected: a lost deal is worth its value with a
    /// LOST outcome, never a negative amount — allowing both would let the same loss be subtracted
    /// twice (once by sign, once by classification) and silently corrupt every total.
    /// </summary>
    public void SetValue(decimal? estimated, decimal? actual)
    {
        if (estimated is < 0) throw new DomainException("ticket.value.negative", "Estimated value cannot be negative.");
        if (actual is < 0) throw new DomainException("ticket.value.negative", "Actual value cannot be negative.");
        EstimatedValue = estimated;
        ActualValue = actual;
    }

    /// <summary>Moderation state (spec §10 zero-trust intake). Defaults to Approved so existing rows,
    /// staff-created tickets, and known-customer submissions need no gate.</summary>
    public TicketApprovalState ApprovalState { get; private set; } = TicketApprovalState.Approved;

    /// <summary>Hold a first-time public submission out of the pool until a staff member approves it.</summary>
    public void MarkPendingApproval() => ApprovalState = TicketApprovalState.Pending;

    public void Approve()
    {
        if (ApprovalState != TicketApprovalState.Pending)
            throw new DomainException("ticket.approve.not_pending", "Only a pending ticket can be approved.");
        ApprovalState = TicketApprovalState.Approved;
    }

    public void Reject()
    {
        if (ApprovalState != TicketApprovalState.Pending)
            throw new DomainException("ticket.reject.not_pending", "Only a pending ticket can be rejected.");
        ApprovalState = TicketApprovalState.Rejected;
    }

    public void Assign(Guid assigneeUserId) => AssignedToId = assigneeUserId;

    public void Unassign() => AssignedToId = null;

    public void SetPriority(Priority priority) => Priority = priority;

    public void SetCategory(Guid? categoryId) => CategoryId = categoryId;

    /// <summary>Repoint the ticket at an equivalent status during a status-set migration (same category,
    /// e.g. when a company forks the global column set into its own). Not a workflow move — no state
    /// machine, no reporting timestamps; identity remap only.</summary>
    public void MigrateStatus(Guid newStatusId) => StatusId = newStatusId;

    public void Edit(string title, string body)
    {
        Title = title.Trim();
        Body = body;
    }

    /// <summary>
    /// Move to <paramref name="to"/> after validating the transition for <paramref name="actor"/>.
    /// <paramref name="from"/> must be the ticket's current status.
    /// </summary>
    public void ChangeStatus(
        TicketStatus from,
        TicketStatus to,
        IReadOnlyCollection<StatusTransition> transitions,
        in StatusChangeActor actor,
        DateTime now)
    {
        if (from.Id != StatusId)
            throw new DomainException("ticket.status.stale", "The ticket's current status has changed; reload and retry.");

        TicketStateMachine.EnsureCanTransition(from, to, transitions, actor);
        ApplyStatus(to, now);
    }

    /// <summary>
    /// Reopen a terminal ticket within the configured window (spec §12/§18.11). Target must be
    /// a non-terminal status. First-resolution time is preserved for reporting; ClosedAt clears.
    /// </summary>
    public void Reopen(TicketStatus from, TicketStatus to, DateTime now, int reopenWindowDays)
    {
        if (from.Id != StatusId)
            throw new DomainException("ticket.status.stale", "The ticket's current status has changed; reload and retry.");
        if (!from.IsTerminal)
            throw new DomainException("ticket.reopen.not_closed", "Only a closed ticket can be reopened.");
        if (to.IsTerminal)
            throw new DomainException("ticket.reopen.target_terminal", "A ticket must reopen into a non-terminal status.");
        if (ClosedAt is null || now > ClosedAt.Value.AddDays(reopenWindowDays))
            throw new DomainException("ticket.reopen.window_expired", "The reopen window has expired; open a new ticket.");

        StatusId = to.Id;
        ClosedAt = null;
    }

    private void ApplyStatus(TicketStatus to, DateTime now)
    {
        StatusId = to.Id;
        switch (to.Category)
        {
            case StatusCategory.Answered when FirstResponseAt is null:
                FirstResponseAt = now;
                break;
            case StatusCategory.Closed:
                ResolvedAt ??= now;
                ClosedAt = now;
                break;
            case StatusCategory.Cancelled:
                ClosedAt = now;
                break;
        }
    }
}
