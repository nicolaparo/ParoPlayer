using System.Security.Cryptography;
using System.Text;

namespace ParoPlayer.Core.Browser;

/// <summary>Outcome of <see cref="BrowserAccess.TryLogin"/>.</summary>
public enum LoginOutcome
{
    Success,
    WrongPin,
    LockedOut,
}

public readonly record struct LoginResult(LoginOutcome Outcome, string? Token = null, TimeSpan RetryAfter = default);

/// <summary>
/// PIN access to the browser session (#26). A new PIN is made for every run of the server; a correct PIN gives a random
/// 128-bit token (sent as a cookie). Wrong guesses are counted per client and in total, and lock out for a while.
/// Tokens expire when idle. All times come from <see cref="TimeProvider"/>.
/// </summary>
public sealed class BrowserAccess(TimeProvider timeProvider)
{
    public const int PinLength = 6;
    public const int MaxFailuresPerClient = 5;
    public const int MaxFailuresTotal = 20;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    private readonly Lock gate = new();
    private readonly Dictionary<string, Failures> failuresByClient = [];
    private readonly Dictionary<string, DateTimeOffset> tokens = [];
    private string pin = string.Empty;
    private int totalFailures;
    private DateTimeOffset totalLockedUntil;

    /// <summary>The PIN to type in the browser; empty while no session is open.</summary>
    public string Pin
    {
        get
        {
            lock (gate)
            {
                return pin;
            }
        }
    }

    /// <summary>Starts a new session: a fresh PIN, and every earlier token and lockout is forgotten. Returns the PIN.</summary>
    public string Open() => Open(RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, PinLength)).ToString($"D{PinLength}"));

    /// <summary>Same as <see cref="Open()"/> with a known PIN (tests).</summary>
    public string Open(string newPin)
    {
        lock (gate)
        {
            pin = newPin;
            Clear();
            return pin;
        }
    }

    /// <summary>Ends the session: no PIN, no valid token.</summary>
    public void Close()
    {
        lock (gate)
        {
            pin = string.Empty;
            Clear();
        }
    }

    /// <summary><paramref name="client"/> identifies the caller (its IP address) for the per-client limit.</summary>
    public LoginResult TryLogin(string client, string? attempt)
    {
        lock (gate)
        {
            var now = timeProvider.GetUtcNow();
            if (pin.Length == 0)
            {
                return new LoginResult(LoginOutcome.WrongPin);
            }

            if (totalLockedUntil > now)
            {
                return new LoginResult(LoginOutcome.LockedOut, RetryAfter: totalLockedUntil - now);
            }

            var failures = failuresByClient.GetValueOrDefault(client);
            if (failures.LockedUntil > now)
            {
                return new LoginResult(LoginOutcome.LockedOut, RetryAfter: failures.LockedUntil - now);
            }

            if (attempt is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(attempt), Encoding.UTF8.GetBytes(pin)))
            {
                failuresByClient.Remove(client);
                var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
                tokens[token] = now;
                return new LoginResult(LoginOutcome.Success, token);
            }

            var count = failures.Count + 1;
            failuresByClient[client] = count >= MaxFailuresPerClient
                ? new Failures(0, now + LockoutDuration)
                : new Failures(count, default);

            if (++totalFailures >= MaxFailuresTotal)
            {
                totalFailures = 0;
                totalLockedUntil = now + LockoutDuration;
            }

            return count >= MaxFailuresPerClient
                ? new LoginResult(LoginOutcome.LockedOut, RetryAfter: LockoutDuration)
                : new LoginResult(LoginOutcome.WrongPin);
        }
    }

    /// <summary>True for a live token; a valid check also counts as activity and keeps it alive.</summary>
    public bool IsValid(string? token)
    {
        if (token is null)
        {
            return false;
        }

        lock (gate)
        {
            if (!tokens.TryGetValue(token, out var lastSeen))
            {
                return false;
            }

            var now = timeProvider.GetUtcNow();
            if (now - lastSeen > IdleTimeout)
            {
                tokens.Remove(token);
                return false;
            }

            tokens[token] = now;
            return true;
        }
    }

    /// <summary>Forgets one token (browser sign out).</summary>
    public void Revoke(string? token)
    {
        if (token is null)
        {
            return;
        }

        lock (gate)
        {
            tokens.Remove(token);
        }
    }

    private void Clear()
    {
        tokens.Clear();
        failuresByClient.Clear();
        totalFailures = 0;
        totalLockedUntil = default;
    }

    private readonly record struct Failures(int Count, DateTimeOffset LockedUntil);
}
