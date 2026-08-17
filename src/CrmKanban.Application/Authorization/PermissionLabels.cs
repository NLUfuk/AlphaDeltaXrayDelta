namespace CrmKanban.Application.Authorization;

/// <summary>
/// Human-readable Turkish labels, one-line descriptions, and scope for permission keys, for the RBAC UI.
/// These are developer-fixed display strings for a fixed key set (not UI-editable business config),
/// so a static map is the minimal correct seam — no DB column, no migration. Unknown keys fall back
/// to the raw key/group so a newly added permission is never rendered blank.
///
/// <para><b>Descriptions are written from the enforcement site, not from the label.</b> Faz 30 found the
/// gap that makes this worth stating: `ticket.view` read like it gated reading and gated nothing, so an
/// admin could switch it off and watch nothing happen. Each sentence below says what actually stops
/// working, and <see cref="IsGlobalOnly"/> marks the two keys no per-company grant can ever satisfy.</para>
/// </summary>
public static class PermissionLabels
{
    private static readonly IReadOnlyDictionary<string, string> Keys = new Dictionary<string, string>
    {
        ["ticket.view"] = "Talepleri görüntüle",
        ["ticket.view.all"] = "Şirketin tüm taleplerini görüntüle",
        ["ticket.edit"] = "Talep düzenle",
        ["ticket.delete"] = "Talep sil",
        ["ticket.assign"] = "Talep ata",
        ["ticket.status.change"] = "Talep durumunu değiştir",
        ["comment.internal"] = "İç not ekle/gör",
        ["report.company"] = "Şirket raporları",
        ["ticket.value"] = "Tutar ve kazanç raporu",
        ["report.global"] = "Genel raporlar (tüm şirketler)",
        ["settings.manage"] = "Ayarları yönet",
        ["status.manage"] = "Durum/kolon yönetimi",
        ["user.invite"] = "Kullanıcı davet et",
        ["permission.assign"] = "Yetki atama",
    };

    /// <summary>One sentence per key: what the user can do with it, and what breaks when it is off.</summary>
    private static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        ["ticket.view"] =
            "Şirketin taleplerini okur: kanban panosu, talep listesi, talep detayı ve onay kuyruğu. " +
            "Kapatılırsa bu şirketin hiçbir talebini göremez; kendi açtığı talepleri görmeye devam eder. " +
            "Hangi talepleri gördüğünü 'Şirketin tüm taleplerini görüntüle' belirler.",
        ["ticket.view.all"] =
            "Kapalıyken kullanıcı yalnız KENDİ çalışma alanını görür: kendisine atanmış talepler ve " +
            "kendi açtığı talepler. Panoda, listede, aramada ve talep detayında geçerlidir — filtre " +
            "sunucuda uygulanır, ekranda gizlenmez. Açıkken şirketin tüm taleplerini görür ve onay " +
            "kuyruğunu kullanabilir. Yazma yetkileri (düzenle/ata/durum) de bu sınırın dışına çıkamaz.",
        ["ticket.edit"] =
            "Talebin başlığını ve içeriğini düzenler, yorumları düzenleyip siler, onay kuyruğundaki " +
            "talebi onaylar veya reddeder.",
        ["ticket.delete"] =
            "Talebi siler. Kayıt veritabanında kalır (geçmiş korunur), listelerden düşer.",
        ["ticket.assign"] =
            "Talebi bir personele atar veya atamayı kaldırır. Atanan kişi o şirketin aktif üyesi olmalıdır.",
        ["ticket.status.change"] =
            "Talebin durumunu değiştirir — kanban'da kartı sürüklemek de budur. Personel rolündeki bir " +
            "kullanıcı ayrıca yalnız kendisine atanmış talepte değiştirebilir.",
        ["comment.internal"] =
            "Müşteriye görünmeyen iç not yazar ve okur. Kapatılırsa yalnız normal yorum yazabilir. " +
            "İç notlar müşteriye hiçbir koşulda gösterilmez; bu yetki personel arasında geçerlidir.",
        ["ticket.value"] =
            "Taleplerin tahmini/gerçekleşen tutarını görür ve girer, Kazanç sekmesini açar. " +
            "Kapatılırsa tutar alanı ekranda görünmez VE sunucu cevabında da boş gelir — gizlenmiş " +
            "değil, verilmemiş olur. Ticari hacim, talebin kendisinden ayrı bir sırdır.",
        ["report.company"] =
            "Bu şirketin raporlarını görüntüler ve CSV olarak dışa aktarır.",
        ["report.global"] =
            "Tüm şirketleri kapsayan genel rapor. YALNIZ SÜPER ADMİNDE ÇALIŞIR — şirket bazında vermek " +
            "hiçbir şey değiştirmez, genel rapor süper admin dışında herkese kapalıdır.",
        ["settings.manage"] =
            "Global sistem ayarları. YALNIZ SÜPER ADMİNDE ÇALIŞIR — ayarlar sistem geneli olduğu için " +
            "şirket bazında vermek hiçbir şey değiştirmez.",
        ["status.manage"] =
            "Şirketin kanban kolonlarını yönetir: ekleme, yeniden adlandırma, renk, sıralama, silme.",
        ["user.invite"] =
            "Bu şirkete personel veya ikinci admin davet eder.",
        ["permission.assign"] =
            "Bu ekranı kullanır: şirketteki kullanıcıların yetkilerini görür ve değiştirir. Yalnızca " +
            "kendi sahip olduğu yetkileri verebilir — sahip olmadığı bir yetkiyi dağıtamaz.",
    };

    /// <summary>
    /// Keys that only mean anything system-wide. The permission row exists (a super admin holds it through
    /// the role baseline), but the code that enforces it asks "is this a super admin?" directly, so a
    /// per-company override can never satisfy it. Offering them as company switches was misleading.
    /// </summary>
    private static readonly IReadOnlySet<string> GlobalScoped = new HashSet<string>(StringComparer.Ordinal)
    {
        "report.global",
        "settings.manage",
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
    public static string DescriptionFor(string key) => Descriptions.GetValueOrDefault(key, "");
    public static bool IsGlobalOnly(string key) => GlobalScoped.Contains(key);
}
