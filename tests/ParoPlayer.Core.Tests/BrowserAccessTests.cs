using Microsoft.Extensions.Time.Testing;
using ParoPlayer.Core.Browser;

namespace ParoPlayer.Core.Tests;

public class BrowserAccessTests
{
    private readonly FakeTimeProvider time = new();
    private readonly BrowserAccess access;

    public BrowserAccessTests()
    {
        access = new BrowserAccess(time);
        access.Open("123456");
    }

    [Fact]
    public void Generated_pin_has_six_digits()
    {
        var pin = new BrowserAccess(time).Open();

        Assert.Equal(BrowserAccess.PinLength, pin.Length);
        Assert.All(pin, c => Assert.True(char.IsAsciiDigit(c)));
    }

    [Fact]
    public void Correct_pin_gives_a_token_that_is_valid()
    {
        var result = access.TryLogin("a", "123456");

        Assert.Equal(LoginOutcome.Success, result.Outcome);
        Assert.Equal(32, result.Token!.Length);
        Assert.True(access.IsValid(result.Token));
    }

    [Fact]
    public void Wrong_pin_or_no_pin_gives_no_token()
    {
        Assert.Equal(LoginOutcome.WrongPin, access.TryLogin("a", "000000").Outcome);
        Assert.Equal(LoginOutcome.WrongPin, access.TryLogin("a", null).Outcome);
        Assert.False(access.IsValid("nope"));
        Assert.False(access.IsValid(null));
    }

    [Fact]
    public void Client_is_locked_out_after_repeated_failures_and_released_after_the_lockout()
    {
        for (var i = 0; i < BrowserAccess.MaxFailuresPerClient - 1; i++)
        {
            Assert.Equal(LoginOutcome.WrongPin, access.TryLogin("a", "000000").Outcome);
        }

        Assert.Equal(LoginOutcome.LockedOut, access.TryLogin("a", "000000").Outcome);
        Assert.Equal(LoginOutcome.LockedOut, access.TryLogin("a", "123456").Outcome);
        Assert.Equal(LoginOutcome.Success, access.TryLogin("b", "123456").Outcome);

        time.Advance(BrowserAccess.LockoutDuration + TimeSpan.FromSeconds(1));

        Assert.Equal(LoginOutcome.Success, access.TryLogin("a", "123456").Outcome);
    }

    [Fact]
    public void Many_failures_from_different_clients_lock_everyone_out()
    {
        for (var i = 0; i < BrowserAccess.MaxFailuresTotal; i++)
        {
            access.TryLogin($"client-{i}", "000000");
        }

        Assert.Equal(LoginOutcome.LockedOut, access.TryLogin("friend", "123456").Outcome);
    }

    [Fact]
    public void Token_expires_when_idle_and_activity_keeps_it_alive()
    {
        var token = access.TryLogin("a", "123456").Token;

        time.Advance(BrowserAccess.IdleTimeout - TimeSpan.FromMinutes(1));
        Assert.True(access.IsValid(token));
        time.Advance(BrowserAccess.IdleTimeout - TimeSpan.FromMinutes(1));
        Assert.True(access.IsValid(token));
        time.Advance(BrowserAccess.IdleTimeout + TimeSpan.FromMinutes(1));
        Assert.False(access.IsValid(token));
    }

    [Fact]
    public void Close_forgets_tokens_and_the_pin()
    {
        var token = access.TryLogin("a", "123456").Token;

        access.Close();

        Assert.False(access.IsValid(token));
        Assert.Equal(string.Empty, access.Pin);
        Assert.Equal(LoginOutcome.WrongPin, access.TryLogin("a", "123456").Outcome);
    }

    [Fact]
    public void Revoke_ends_one_token()
    {
        var first = access.TryLogin("a", "123456").Token;
        var second = access.TryLogin("b", "123456").Token;

        access.Revoke(first);

        Assert.False(access.IsValid(first));
        Assert.True(access.IsValid(second));
    }
}
