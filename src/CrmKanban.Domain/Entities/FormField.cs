using CrmKanban.Domain.Common;
using CrmKanban.Domain.Enums;

namespace CrmKanban.Domain.Entities;

/// <summary>
/// A configurable extra field on a company's public request form (spec §4.6/§9). The set of fields is
/// per company, admin-editable — the data-driven seam for "the form fields are configurable" rather than
/// a hard-coded form. Submitted values are captured on the ticket (denormalized as label+value JSON), so
/// this definition can change later without rewriting history.
/// </summary>
public sealed class FormField : Entity
{
    private FormField() { } // EF

    public FormField(Guid companyId, string label, FormFieldType type, bool required, int sortOrder, string? options = null)
    {
        CompanyId = companyId;
        Label = label.Trim();
        Type = type;
        Required = required;
        SortOrder = sortOrder;
        Options = NormalizeOptions(type, options);
    }

    public Guid CompanyId { get; private set; }
    public string Label { get; private set; } = null!;
    public FormFieldType Type { get; private set; }
    public bool Required { get; private set; }
    public int SortOrder { get; private set; }
    /// <summary>For Select: newline-separated options. Null otherwise.</summary>
    public string? Options { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void Update(string label, FormFieldType type, bool required, int sortOrder, string? options)
    {
        Label = label.Trim();
        Type = type;
        Required = required;
        SortOrder = sortOrder;
        Options = NormalizeOptions(type, options);
    }

    public void SetActive(bool active) => IsActive = active;

    private static string? NormalizeOptions(FormFieldType type, string? options) =>
        type == FormFieldType.Select ? (string.IsNullOrWhiteSpace(options) ? null : options.Trim()) : null;
}
