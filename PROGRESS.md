# AlphaDeltaXrayDelta — Progress

| Alan | Deger |
|---|---|
| Son guncelleme | 2026-07-28 |
| Aktif faz | Faz 0 — Repo kurulumu |
| Genel durum | Iskelet asamasi, uygulama kodu yok |
| Remote | https://github.com/NLUfuk/AlphaDeltaXrayDelta.git |
| Ana branch | `main` |

## Fazlar

### Faz 0 — Repo kurulumu ve hijyen ✅
- [x] Ic ice duplike git deposu kaldirildi (`AlphaDeltaXrayDelta/.git`) — proje koku artik repo kokudur
- [x] Git kimligi GitHub hesabiyla hizalandi (`user.email = ufukf1998@gmail.com`)
- [x] `.gitignore` eklendi (build ciktisi, IDE, secret, node, OS gurultusu)
- [x] `PROGRESS.md` olusturuldu
- [x] Ilk commit ve `origin/main` push

### Faz 1 — Proje iskeleti ❌
- [ ] Teknoloji yigini kararlastirilacak (henuz belirlenmedi)
- [ ] Cozum/proje yapisi olusturulacak (Clean Architecture katmanlari)
- [ ] Kod stili ve analiz kurallari (`.editorconfig`, linter/analyzer)
- [ ] Test projesi ve ilk test kosumu
- [ ] CI pipeline (build + test)

## Bilinen sorunlar / teknik borc

| # | Aciklama | Oncelik |
|---|---|---|
| 1 | Teknoloji yigini belirlenmedi; `.gitignore` .NET + Node varsayimiyla genis tutuldu, yigin netlesince daraltilmali | Yuksek |
| 2 | Repo kok dizininin adi `Yeni klasör` — ASCII disi karakter bazi CLI/CI araclarinda yol sorunu cikarabilir; dizin yeniden adlandirilmali | Orta |
| 3 | `Yeni Metin Belgesi.txt` (0 byte) kok dizinde duruyor, takip edilmiyor — silinmeli veya icerik kazandirilmali | Dusuk |
| 4 | Branch koruma / PR akisi tanimlanmadi (`main` dogrudan push'a acik) | Dusuk |

## Ortam gereksinimleri

| Gereksinim | Durum |
|---|---|
| GitHub kimlik dogrulama (`origin` push yetkisi) | Gerekli |
| Uygulama secret / connection string | Henuz yok — geldiginde `.env` veya user-secrets ile yonetilecek, repoya girmez |
