using System.Globalization;
using CrmKanban.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CrmKanban.Application.Reports;

/// <summary>
/// Renders a <see cref="TicketReport"/> as a PDF (Faz 41), replacing the ticket-level CSV export.
/// <para>
/// Why PDF and not CSV/xlsx: the thing being exported is a REPORT — something printed, mailed to a
/// manager, filed. The old CSV was a raw ticket dump with bare GUIDs in the assignee and category
/// columns, unreadable without the database next to it. A report that answers "which customers cost
/// us the most time and what did they bring in" has to carry its own labels, totals and units.
/// </para>
/// <para>
/// A4 LANDSCAPE on purpose: the customer table carries eleven columns. In portrait they either
/// collapse into unreadable slivers or force a font size nobody can read on paper.
/// </para>
/// <para>
/// Money columns are dropped ENTIRELY when <see cref="TicketReport.Revenue"/> is null (the caller
/// lacks <c>ticket.value</c>) — not rendered blank. A blank money column in a printed table reads as
/// "zero", and a report that quietly understates revenue is worse than one that omits it.
/// </para>
/// </summary>
public static class ReportPdf
{
    /// <summary>
    /// QuestPDF requires a declared licence tier before it renders anything. Declared HERE rather than
    /// at application startup on purpose: startup only runs in the API host, so a startup-only
    /// declaration works in production and throws in tests — the gap this static constructor closes.
    /// <para>
    /// LICENCE OBLIGATION: Community is free while annual gross revenue stays below 1M USD. Above that
    /// threshold a paid licence must be bought and this line changed. See docs/PROGRESS.md, borç #46.
    /// </para>
    /// </summary>
    static ReportPdf() => QuestPDF.Settings.License = LicenseType.Community;

    // Explicit tr-TR: the PDF is a Turkish-language document, so its numbers and dates must not
    // follow whatever culture the server process happens to run under.
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private const string Ink = "#0F2540";
    private const string Muted = "#64748B";
    private const string Line = "#E2E8F0";
    private const string Accent = "#F59E0B";
    private const string Won = "#1BAF7A";
    private const string Lost = "#D64550";

    /// <param name="scopeName">Company name, or null for the super admin's all-companies report.</param>
    /// <param name="brandName">Settings <c>brand.system_name</c> — the operator may have renamed the system.</param>
    /// <param name="generatedAt">Passed in rather than read from the clock here so the document stays
    /// a pure function of its inputs and the tests can assert on it.</param>
    public static byte[] Render(TicketReport report, string? scopeName, string brandName, DateTime generatedAt)
    {
        var money = report.Revenue;

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(t => t.FontSize(8.5f).FontColor(Ink).FontFamily(Fonts.Lato));

                page.Header().Element(c => Header(c, report, scopeName, brandName, generatedAt));
                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(14);
                    col.Item().Element(c => GeneralStats(c, report));
                    if (money is not null) col.Item().Element(c => Financials(c, money));
                    col.Item().Element(c => Customers(c, report, money?.Currency));
                });
                page.Footer().Element(c => Footer(c, brandName));
            });
        }).GeneratePdf();
    }

    private static void Header(IContainer c, TicketReport r, string? scopeName, string brandName, DateTime at) =>
        c.BorderBottom(1).BorderColor(Line).PaddingBottom(8).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(brandName).FontSize(16).Bold().FontColor(Ink);
                col.Item().Text("Müşteri ve talep raporu").FontSize(10).FontColor(Muted);
            });
            row.ConstantItem(260).AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text(scopeName ?? "Tüm şirketler").FontSize(11).SemiBold();
                col.Item().AlignRight().Text(Period(r.From, r.To)).FontColor(Muted);
                col.Item().AlignRight().Text($"Oluşturulma: {at.ToString("dd.MM.yyyy HH:mm", Tr)}").FontColor(Muted);
            });
        });

    /// <summary>"Tüm zamanlar" when unbounded — an empty date line would read as a missing value.</summary>
    private static string Period(DateTime? from, DateTime? to) => (from, to) switch
    {
        (null, null) => "Tüm zamanlar",
        ({ } f, null) => $"{f.ToString("dd.MM.yyyy", Tr)} — bugün",
        (null, { } t) => $"başlangıçtan {t.ToString("dd.MM.yyyy", Tr)} tarihine",
        var (f, t) => $"{f!.Value.ToString("dd.MM.yyyy", Tr)} — {t!.Value.ToString("dd.MM.yyyy", Tr)}",
    };

    private static void GeneralStats(IContainer c, TicketReport r) =>
        c.Column(col =>
        {
            col.Item().Element(SectionTitle).Text("Genel istatistikler");
            col.Item().PaddingTop(6).Row(row =>
            {
                row.Spacing(8);
                Tile(row, "Toplam talep", r.TotalTickets.ToString(Tr), null);
                foreach (var s in r.ByStatusCategory)
                    Tile(row, CategoryLabel(s.Category), s.Count.ToString(Tr), null);
                // The sample size rides along with each average: they are counted over different
                // ticket sets (answered vs. resolved), so side by side without their denominators
                // they invite the reading "çözüm ilk yanıttan hızlı, bu rapor bozuk".
                Tile(row, "Ort. ilk yanıt", Hours(r.AvgFirstResponseHours), $"saat · {r.FirstResponseCount} talep");
                Tile(row, "Ort. çözüm", Hours(r.AvgResolutionHours), $"saat · {r.ResolutionCount} talep");
            });
        });

    private static void Financials(IContainer c, RevenueSummary m) =>
        c.Column(col =>
        {
            col.Item().Element(SectionTitle).Text("Mali durum");
            col.Item().PaddingTop(6).Row(row =>
            {
                row.Spacing(8);
                Tile(row, "Kazanılan", Money(m.WonTotal, m.Currency), $"{m.WonCount} talep", Won);
                Tile(row, "Kaybedilen", Money(m.LostTotal, m.Currency), $"{m.LostCount} talep", Lost);
                Tile(row, "Açık hat", Money(m.OpenTotal, m.Currency), $"{m.OpenCount} talep", Accent);
                // "%100" over one decided ticket is true and useless; the sample size says which it is.
                Tile(row, "Kazanma oranı", Percent(m.WinRateByCount),
                    $"{m.WonCount + m.LostCount} talep · tutarca {Percent(m.WinRateByValue)}");
                Tile(row, "Tahmin isabeti", Percent(m.ForecastAccuracy is { } f ? (double)f : null), "%100 = tam isabet");
                Tile(row, "Tutarsız talep", m.UnpricedCount.ToString(Tr), "toplama katılmıyor");
            });
        });

    private static void Customers(IContainer c, TicketReport r, string? currency) =>
        c.Column(col =>
        {
            col.Item().Element(SectionTitle).Text("Müşteri kırılımı");
            col.Item().PaddingTop(2).Text(
                    "Yalnız müşterilerin açtığı talepler; personel ve yöneticilerin açtıkları sayılmaz. " +
                    "Süreler talebin açılışından çözümüne kadar GEÇEN takvim süresidir, çalışılan saat kaydı tutulmaz.")
                .FontSize(7.5f).FontColor(Muted).Italic();

            if (r.Customers.Count == 0)
            {
                col.Item().PaddingTop(10).Text("Bu dönemde müşteri talebi yok.").FontColor(Muted);
                return;
            }

            col.Item().PaddingTop(6).Table(table =>
            {
                table.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(3.2f);   // müşteri
                    cd.RelativeColumn(3.4f);   // e-posta
                    cd.ConstantColumn(38);     // talep
                    cd.ConstantColumn(34);     // açık
                    cd.ConstantColumn(48);     // kazanılan
                    cd.ConstantColumn(50);     // kaybedilen
                    if (currency is not null) { cd.ConstantColumn(74); cd.ConstantColumn(74); }
                    cd.ConstantColumn(56);     // ort. çözüm
                    cd.ConstantColumn(58);     // toplam süre
                    cd.ConstantColumn(56);     // ort. ilk yanıt
                    cd.ConstantColumn(58);     // son talep
                });

                table.Header(h =>
                {
                    Th(h, "Müşteri", left: true);
                    Th(h, "E-posta", left: true);
                    Th(h, "Talep");
                    Th(h, "Açık");
                    Th(h, "Kazanılan");
                    Th(h, "Kaybedilen");
                    if (currency is not null) { Th(h, $"Kazanılan ({currency})"); Th(h, $"Açık ({currency})"); }
                    Th(h, "Ort. çözüm (sa)");
                    Th(h, "Toplam süre (sa)");
                    Th(h, "Ort. ilk yanıt (sa)");
                    Th(h, "Son talep");
                });

                foreach (var x in r.Customers)
                {
                    Td(table, x.Name, left: true);
                    Td(table, x.Email, left: true, muted: true);
                    Td(table, x.TicketCount.ToString(Tr));
                    Td(table, x.OpenCount.ToString(Tr));
                    Td(table, x.WonCount.ToString(Tr));
                    Td(table, x.LostCount.ToString(Tr));
                    if (currency is not null)
                    {
                        Td(table, Amount(x.WonTotal));
                        Td(table, Amount(x.OpenTotal));
                    }
                    Td(table, Hours(x.AvgResolutionHours));
                    Td(table, Hours(x.TotalResolutionHours));
                    Td(table, Hours(x.AvgFirstResponseHours));
                    Td(table, x.LastTicketAt.ToString("dd.MM.yyyy", Tr));
                }
            });

            // Totals belong on the page, not in the reader's head: the per-customer rows are the
            // evidence, this line is the answer.
            // Labelled "müşteri talepleri" on purpose — this total legitimately differs from "Mali
            // durum" above, which counts EVERY ticket including staff- and admin-opened ones. Two
            // different numbers with the same label is how a reader learns to distrust a report.
            col.Item().PaddingTop(6).Text(t =>
            {
                t.Span("Toplam (yalnız müşteri talepleri): ").SemiBold();
                t.Span($"{r.Customers.Count} müşteri · {r.Customers.Sum(x => x.TicketCount)} talep");
                if (currency is not null)
                    t.Span($" · kazanılan {Money(r.Customers.Sum(x => x.WonTotal ?? 0m), currency)}" +
                           $" · açık {Money(r.Customers.Sum(x => x.OpenTotal ?? 0m), currency)}");
            });
        });

    private static void Footer(IContainer c, string brandName) =>
        c.BorderTop(1).BorderColor(Line).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text(brandName).FontSize(7.5f).FontColor(Muted);
            row.RelativeItem().AlignRight().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Muted));
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        });

    // ---- small building blocks ----

    private static IContainer SectionTitle(IContainer c) =>
        c.BorderLeft(3).BorderColor(Accent).PaddingLeft(6).DefaultTextStyle(t => t.FontSize(11).SemiBold());

    private static void Tile(RowDescriptor row, string label, string value, string? sub, string? accent = null) =>
        row.RelativeItem().Border(1).BorderColor(Line).Padding(7).Column(col =>
        {
            col.Item().Text(label).FontSize(7).FontColor(Muted);
            col.Item().Text(value).FontSize(13).SemiBold().FontColor(accent ?? Ink);
            if (sub is not null) col.Item().Text(sub).FontSize(7).FontColor(Muted);
        });

    private static void Th(TableCellDescriptor h, string text, bool left = false) =>
        h.Cell().Background("#F1F5F9").BorderBottom(1).BorderColor(Line).Padding(4)
            .AlignedTo(left).Text(text).FontSize(7).SemiBold().FontColor(Muted);

    private static void Td(TableDescriptor t, string text, bool left = false, bool muted = false) =>
        t.Cell().BorderBottom(1).BorderColor(Line).Padding(4)
            .AlignedTo(left).Text(text).FontSize(8).FontColor(muted ? Muted : Ink);

    private static IContainer AlignedTo(this IContainer c, bool left) => left ? c.AlignLeft() : c.AlignRight();

    // Kept word-for-word in sync with the SPA's STATUS_CATEGORIES (frontend/src/lib/messages.ts).
    // A printed report that names the same category differently from the screen it was exported from
    // sends the reader looking for a difference that does not exist.
    private static string CategoryLabel(StatusCategory c) => c switch
    {
        StatusCategory.Open => "Açık",
        StatusCategory.Pending => "Beklemede",
        StatusCategory.Answered => "Yanıtlandı",
        StatusCategory.Waiting => "Müşteri Bekleniyor",
        StatusCategory.Closed => "Kapandı",
        StatusCategory.Cancelled => "İptal",
        _ => c.ToString(),
    };

    // "—" rather than "0" for every missing figure: nothing measured is not the same as measured zero,
    // and this is the one distinction a printed report can never explain after the fact.
    private static string Hours(double? h) => h is null ? "—" : h.Value.ToString("N1", Tr);
    private static string Amount(decimal? d) => d is null ? "—" : d.Value.ToString("N0", Tr);
    private static string Money(decimal d, string currency) => $"{d.ToString("N0", Tr)} {currency}";
    private static string Percent(double? v) => v is null ? "—" : $"%{(v.Value * 100).ToString("N1", Tr)}";
}
