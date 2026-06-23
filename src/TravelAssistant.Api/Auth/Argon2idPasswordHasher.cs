using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace TravelAssistant.Api.Auth;

// LOGIN-001 §7-A — Argon2id m=19456 (19 MiB), t=2, p=1, hashLen=32, saltLen=16.
// PHC-string format: $argon2id$v=19$m=19456,t=2,p=1$<salt>$<hash>
//
// §8 #3: Argon2id is the ONLY hash. NO BCrypt anywhere in Auth/. Semgrep
// `login-must-use-argon2id-not-bcrypt` blocks BCrypt patterns in this folder.
//
// §8 #4: SemaphoreSlim(8) caps concurrent verifies — overflow returns 503,
// NOT 401 (so the gate can distinguish capacity-exhaustion from credential
// failures). The literal `SemaphoreSlim(8` appears below so the contract
// invariant `grep -rqE 'SemaphoreSlim\s*\(\s*8\s*[,)]' src/**/Auth/` passes.
public interface IPasswordHasher
{
    Task<bool> VerifyAsync(string password, string phcHash, CancellationToken ct = default);
    Task<bool> VerifyDummyAsync(string password, CancellationToken ct = default);
    string Hash(string password);
}

public sealed class Argon2idPasswordHasher : IPasswordHasher, IDisposable
{
    private const int MemoryKb = 19456;
    private const int Iterations = 2;
    private const int Parallelism = 1;
    private const int HashLength = 32;
    private const int SaltLength = 16;

    // §I1 — unknown-user dummy verify must be CPU-equivalent to a real verify.
    // Computed once at startup from a fixed-but-non-sensitive plaintext.
    private static readonly string DummyHash = ComputeStartupDummyHash();

    // §8 #4 — argon2id concurrency cap. The `8` literal is load-bearing for the gate.
    private readonly SemaphoreSlim _gate = new SemaphoreSlim(8, 8);

    public async Task<bool> VerifyAsync(string password, string phcHash, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(phcHash);
        if (!await _gate.WaitAsync(0, ct).ConfigureAwait(false))
            throw new Argon2OverflowException();
        try
        {
            return VerifyCore(password, phcHash);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> VerifyDummyAsync(string password, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(password);
        // §I1 — same code path, same cost. Result is discarded by caller.
        if (!await _gate.WaitAsync(0, ct).ConfigureAwait(false))
            throw new Argon2OverflowException();
        try
        {
            _ = VerifyCore(password, DummyHash);
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var hash = ComputeArgon2id(Encoding.UTF8.GetBytes(password.Normalize(NormalizationForm.FormC)), salt);
        return $"$argon2id$v=19$m={MemoryKb},t={Iterations},p={Parallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool VerifyCore(string password, string phcHash)
    {
        var parts = phcHash.Split('$');
        if (parts.Length != 6 || parts[1] != "argon2id") return false;
        var saltBytes = Convert.FromBase64String(parts[4]);
        var expected = Convert.FromBase64String(parts[5]);
        var actual = ComputeArgon2id(Encoding.UTF8.GetBytes(password.Normalize(NormalizationForm.FormC)), saltBytes);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static byte[] ComputeArgon2id(byte[] password, byte[] salt)
    {
        using var argon2 = new Argon2id(password)
        {
            Salt = salt,
            DegreeOfParallelism = Parallelism,
            Iterations = Iterations,
            MemorySize = MemoryKb,
        };
        return argon2.GetBytes(HashLength);
    }

    private static string ComputeStartupDummyHash()
    {
        var salt = new byte[SaltLength]; // fixed zero salt is fine — value is non-sensitive
        var pwd = Encoding.UTF8.GetBytes("dummy-startup-pad-not-a-real-password");
        var bytes = ComputeArgon2id(pwd, salt);
        return $"$argon2id$v=19$m={MemoryKb},t={Iterations},p={Parallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(bytes)}";
    }

    public void Dispose() => _gate.Dispose();
}

public sealed class Argon2OverflowException : Exception
{
    public Argon2OverflowException() : base("Argon2id concurrency cap reached.") { }
}
