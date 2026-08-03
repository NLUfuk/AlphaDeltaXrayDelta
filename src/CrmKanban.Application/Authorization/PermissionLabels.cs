namespace CrmKanban.Application.Authorization;

/// <summary>
/// Human-readable Turkish labels for permission keys and their group prefixes, for the RBAC UI.
/// These are developer-fixed display strings for a fixed key set (not UI-editable business config),
/// so a static map is the minimal correct seam — no DB column, no migration. Unknown keys fall back
/// to the raw key/group so a newly added permission is never rendered blank.
/// </summary>
public static class PermissionLabels
{
    private static readonly IReadOnlyDictionary<string, string> Keys = new Dictionary<string, string>
    {
        ["ticket.view"] = "Talepleri görüntüle",
        ["ticket.edit"] = "Talep düzenle",
        ["ticket.delete"] = "Talep sil",
        ["ticket.assign"] = "Talep ata",
        ["ticket.status.change"] = "Talep durumunu değiştir",
        ["comment.internal"] = "İç not ekle/gör",
        ["report.company"] = "Şirket raporları",
        ["report.global"] = "Genel raporlar (tüm şirketler)",
        ["settings.manage"] = "Ayarları yönet",
        ["status.manage"] = "Durum/kolon yönetimi",
        ["user.invite"] = "Kullanıcı davet et",
        ["permission.assign"] = "Yetki atama",
    };

    private static readonly IReadOnlyDictionary<string, string> Groups = new Dictionary<string, string>
    {
        ["ticket"] = "Talepler",
        ["comment"] = "Yorumlar",
        ["report"] = "Raporlar",
        ["settings"] = "Ayarlar",
        ["status"] = "Durumlar",
        ["user"] = "Kullanıcılar",
        ["permission"] = "Yetkiler",
    };

    public static string ForKey(string key) => Keys.GetValueOrDefault(key, key);
    public static string ForGroup(string group) => Groups.GetValueOrDefault(group, group);
}
