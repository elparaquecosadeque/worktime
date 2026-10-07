using Worktime.Application.Common.Interfaces;

namespace Worktime.Infrastructure.Users;

internal sealed class PasswordHasherAdapter : IPasswordHasher
{
    // PBKDF2 with per-hash salt and automatic upgrade marker; we never roll our own crypto.
    private readonly Microsoft.AspNetCore.Identity.PasswordHasher<object> _inner = new();
    private static readonly object Subject = new();

    public string Hash(string password) => _inner.HashPassword(Subject, password);

    public bool Verify(string hash, string password) =>
        _inner.VerifyHashedPassword(Subject, hash, password) != Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed;
}
