using System.Security.Cryptography;
using System.Text;

namespace ParoPlayer.Core.Transfer;

/// <summary>
/// Approval and session state of the Wi-Fi transfer: one device, one pending request, 4-digit code,
/// 128-bit tokens, rate limit and idle timeout. The UI approves, denies and disconnects here.
/// Events can be raised on a server thread, marshal to the UI thread before touching components.
/// </summary>
public sealed class TransferSessions(TimeProvider timeProvider, TransferOptions options)
{
    private readonly object gate = new();
    private readonly Queue<DateTimeOffset> recentRequests = new();
    private AccessRequest? request;
    private AccessStatus requestStatus;
    private DateTimeOffset requestedAt;
    private string? token;
    private bool tokenDelivered;
    private DateTimeOffset lastActivity;

    /// <summary>Raised when a device asks for access; the UI shows Allow/Deny.</summary>
    public event Action<AccessRequest>? AccessRequested;

    /// <summary>Raised whenever <see cref="State"/> changes (also on idle expiry and disconnect).</summary>
    public event Action? StateChanged;

    public TransferState State
    {
        get
        {
            lock (gate)
            {
                Refresh();
                return StateNow();
            }
        }
    }

    /// <summary>The request waiting for Allow/Deny, or null.</summary>
    public AccessRequest? PendingRequest
    {
        get
        {
            lock (gate)
            {
                Refresh();
                return StateNow() == TransferState.Pending ? request : null;
            }
        }
    }

    /// <summary>The connected device, or null.</summary>
    public AccessRequest? ConnectedDevice
    {
        get
        {
            lock (gate)
            {
                Refresh();
                return StateNow() == TransferState.Connected ? request : null;
            }
        }
    }

    public AccessResult RequestAccess(string deviceName, string address)
    {
        AccessRequest created;
        lock (gate)
        {
            var now = timeProvider.GetUtcNow();
            Refresh();
            switch (StateNow())
            {
                case TransferState.Connected:
                    return new(AccessOutcome.Connected, null);
                case TransferState.Pending:
                    return new(AccessOutcome.RateLimited, null);
            }

            while (recentRequests.Count > 0 && now - recentRequests.Peek() >= TimeSpan.FromMinutes(1))
            {
                recentRequests.Dequeue();
            }

            if (recentRequests.Count >= options.MaxAccessRequestsPerMinute)
            {
                return new(AccessOutcome.RateLimited, null);
            }

            recentRequests.Enqueue(now);
            created = new(RandomHex(), deviceName, address, RandomNumberGenerator.GetInt32(10000).ToString("D4"));
            request = created;
            requestStatus = AccessStatus.Pending;
            requestedAt = now;
            token = null;
            tokenDelivered = false;
        }

        StateChanged?.Invoke();
        AccessRequested?.Invoke(created);
        return new(AccessOutcome.Created, created);
    }

    /// <summary>Status of a request, or null when unknown. The session token is returned exactly once, on the first poll after approval.</summary>
    public AccessPoll? Poll(string requestId)
    {
        lock (gate)
        {
            Refresh();
            if (request is null || !SecureEquals(request.RequestId, requestId))
            {
                return null;
            }

            if (requestStatus == AccessStatus.Approved && !tokenDelivered)
            {
                tokenDelivered = true;
                return new(AccessStatus.Approved, token);
            }

            return new(requestStatus, null);
        }
    }

    public bool Approve() => Answer(AccessStatus.Approved);

    public bool Deny() => Answer(AccessStatus.Denied);

    /// <summary>Ends the session or cancels the pending request.</summary>
    public void Disconnect()
    {
        lock (gate)
        {
            if (request is null)
            {
                return;
            }

            Clear();
        }

        StateChanged?.Invoke();
    }

    /// <summary>True when the token belongs to the live session. Counts as activity.</summary>
    public bool Validate(string? candidate)
    {
        lock (gate)
        {
            Refresh();
            if (string.IsNullOrEmpty(candidate) || token is null || StateNow() != TransferState.Connected || !SecureEquals(token, candidate))
            {
                return false;
            }

            lastActivity = timeProvider.GetUtcNow();
            return true;
        }
    }

    /// <summary>Applies the idle and pending timeouts now. The server calls this on a timer.</summary>
    public void ExpireIfIdle()
    {
        bool changed;
        lock (gate)
        {
            var before = StateNow();
            Refresh();
            changed = before != StateNow();
        }

        if (changed)
        {
            StateChanged?.Invoke();
        }
    }

    private bool Answer(AccessStatus answer)
    {
        lock (gate)
        {
            Refresh();
            if (StateNow() != TransferState.Pending)
            {
                return false;
            }

            requestStatus = answer;
            if (answer == AccessStatus.Approved)
            {
                token = RandomHex();
                lastActivity = timeProvider.GetUtcNow();
            }
        }

        StateChanged?.Invoke();
        return true;
    }

    private TransferState StateNow() => request is null
        ? TransferState.Idle
        : requestStatus switch
        {
            AccessStatus.Pending => TransferState.Pending,
            AccessStatus.Approved => TransferState.Connected,
            _ => TransferState.Idle,
        };

    private void Refresh()
    {
        var now = timeProvider.GetUtcNow();
        if (requestStatus == AccessStatus.Pending && request is not null && now - requestedAt >= options.PendingTimeout)
        {
            requestStatus = AccessStatus.Denied;
        }
        else if (requestStatus == AccessStatus.Approved && now - lastActivity >= options.IdleTimeout)
        {
            Clear();
        }
    }

    private void Clear()
    {
        request = null;
        requestStatus = AccessStatus.Pending;
        token = null;
        tokenDelivered = false;
    }

    private static string RandomHex() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    private static bool SecureEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
