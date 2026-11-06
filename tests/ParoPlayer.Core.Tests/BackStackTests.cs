using ParoPlayer.Core.Navigation;

namespace ParoPlayer.Core.Tests;

public class BackStackTests
{
    [Fact]
    public void Press_WithNoLayers_ReturnsNull() => Assert.Null(new BackStack().PressAsync());

    [Fact]
    public async Task Press_RunsNewestLayerOnly()
    {
        var stack = new BackStack();
        List<string> calls = [];
        stack.Register(() => { calls.Add("selection"); return Task.CompletedTask; });
        stack.Register(() => { calls.Add("sheet"); return Task.CompletedTask; });

        await stack.PressAsync()!;

        Assert.Equal(["sheet"], calls);
    }

    [Fact]
    public async Task Press_AfterNewestLayerCloses_RunsTheOneBelow()
    {
        var stack = new BackStack();
        List<string> calls = [];
        stack.Register(() => { calls.Add("selection"); return Task.CompletedTask; });
        stack.Register(() => { calls.Add("sheet"); return Task.CompletedTask; }).Dispose();

        await stack.PressAsync()!;

        Assert.Equal(["selection"], calls);
    }

    [Fact]
    public void Press_AfterLastLayerCloses_FallsThroughToPage()
    {
        var stack = new BackStack();
        stack.Register(() => Task.CompletedTask).Dispose();

        Assert.Null(stack.PressAsync());
    }
}
