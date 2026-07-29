namespace CrmKanban.Domain.Enums;

/// <summary>
/// Semantic status category. Code and reports branch on THIS, never on the display
/// name — names are super-admin editable (spec §4.3, §12). Statuses are data-driven
/// (TicketStatus rows); every row maps to exactly one category.
/// </summary>
public enum StatusCategory
{
    Open = 0,
    Pending = 1,
    Answered = 2,
    Waiting = 3,
    Closed = 4,
    Cancelled = 5,
}

/// <summary>Ticket priority — 4 levels, default Normal, set by staff not customer (spec §18.17).</summary>
public enum Priority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3,
}

/// <summary>System roles (spec §8). Personel exists because tickets are assigned to staff (§18.2).</summary>
public enum RoleType
{
    SuperAdmin = 0,
    Admin = 1,
    Personel = 2,
    Customer = 3,
}

/// <summary>User-level permission override relative to role baseline (spec §7).</summary>
public enum UserPermissionType
{
    Grant = 0,
    Deny = 1,
}

/// <summary>How a comment entered the system (spec §11, §18.16). Email-to-ticket is v2; the field
/// exists now so adding it later is cheap.</summary>
public enum CommentSource
{
    Web = 0,
    Email = 1,
}

/// <summary>Lifecycle of a queued email (spec §11, §14). Sending happens in a background worker,
/// never the request loop; failures retry until DeadLetter.</summary>
public enum EmailStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,      // transient failure, will retry
    DeadLetter = 3,  // gave up after max attempts
}

/// <summary>Audit/report/mail source events on a ticket (spec §11, §14). Notifications are driven
/// from these, not from mail calls sprinkled through business logic.</summary>
public enum TicketEventType
{
    Created = 0,
    StatusChanged = 1,
    Assigned = 2,
    Unassigned = 3,
    CommentAdded = 4,
    InternalNoteAdded = 5,
    PriorityChanged = 6,
    CategoryChanged = 7,
    Reopened = 8,
    Edited = 9,
    Deleted = 10,
}
