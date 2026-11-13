using System.Security.Cryptography;

namespace Api.Services;

public static class InviteCodes
{
    // No 0/O, 1/I/L: codes are read out loud and typed on phones
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    public const int Length = 8;
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    /// <summary>Cryptographically random, so codes can't be guessed from earlier ones.</summary>
    public static string Generate() => RandomNumberGenerator.GetString(Alphabet, Length);

    public static string Normalize(string code) => code.Trim().ToUpperInvariant().Replace("-", "").Replace(" ", "");
}
