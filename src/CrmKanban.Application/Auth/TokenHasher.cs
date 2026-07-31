using System.Security.Cryptography;
using System.Text;

namespace CrmKanban.Application.Auth;

/// <summary>
/// One-time token hashing (spec §9): opaque tokens (invite / account activation) are stored hashed,
/// like refresh tokens — the raw value only ever leaves the server in an email. SHA-256 is fine here
/// because the token is a 256-bit random value, not a low-entropy secret.
/// </summary>
public static class TokenHasher
{
    public static string Hash(string raw) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    /// <summary>A fresh opaque single-use token (256 bits, url-safe hex).</summary>
    public static string NewRawToken() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
}
