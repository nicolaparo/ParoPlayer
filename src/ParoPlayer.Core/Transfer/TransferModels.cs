namespace ParoPlayer.Core.Transfer;

public enum TransferState
{
    /// <summary>No device is connected or waiting.</summary>
    Idle,

    /// <summary>A device asked for access and the user has not answered yet.</summary>
    Pending,

    /// <summary>A device is approved and holds the session.</summary>
    Connected,
}

public enum AccessStatus
{
    Pending,
    Approved,
    Denied,
}

public enum AccessOutcome
{
    Created,

    /// <summary>Another device is connected (HTTP 409).</summary>
    Connected,

    /// <summary>Another request is pending or too many requests were made (HTTP 429).</summary>
    RateLimited,
}

/// <summary>What the phone shows to the user: who asks and the code that must match the one in the browser.</summary>
public sealed record AccessRequest(string RequestId, string DeviceName, string Address, string Code);

public sealed record AccessResult(AccessOutcome Outcome, AccessRequest? Request);

/// <summary>Poll answer. <see cref="Token"/> is set only on the first poll after approval.</summary>
public sealed record AccessPoll(AccessStatus Status, string? Token);
