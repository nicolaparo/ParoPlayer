using System.ComponentModel;
using ParoPlayer.Core.Download;

namespace ParoPlayer.Core.Tests;

public sealed class AudioConverterTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public AudioConverterTests() => Directory.CreateDirectory(dir);

    public void Dispose() => Directory.Delete(dir, recursive: true);

    private sealed class FakeLocator(string? path) : IFfmpegLocator
    {
        public string? ExecutablePath { get; } = path;
    }

    private sealed class FakeRunner(Func<IReadOnlyList<string>, ProcessResult> run) : IProcessRunner
    {
        public string? FileName { get; private set; }

        public IReadOnlyList<string> Arguments { get; private set; } = [];

        public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
        {
            FileName = fileName;
            Arguments = arguments;
            return Task.FromResult(run(arguments));
        }
    }

    private string Input(string name = "in.webm")
    {
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, "audio");
        return path;
    }

    [Fact]
    public void Arguments_CopyAac_AndEncodeOtherCodecs()
    {
        var copy = FfmpegArguments.ToM4a("in.mp4", "out.m4a", inputIsAac: true);
        var encode = FfmpegArguments.ToM4a("in.webm", "out.m4a", inputIsAac: false);

        Assert.Equal(["-c:a", "copy"], copy.SkipWhile(a => a != "-c:a").Take(2));
        Assert.Equal(["-c:a", "aac", "-b:a", "192k"], encode.SkipWhile(a => a != "-c:a").Take(4));
        Assert.Equal("out.m4a", encode[^1]);
        Assert.Contains("-vn", copy);
    }

    [Fact]
    public async Task Ffmpeg_RunsExecutableAndReplacesInput()
    {
        var input = Input("in.mp4");
        var runner = new FakeRunner(args =>
        {
            File.WriteAllText(args[^1], "converted");
            return new ProcessResult(0, "");
        });
        var converter = new FfmpegAudioConverter(new FakeLocator("/native/libffmpeg.so"), runner, new PassThroughAudioConverter());

        var output = await converter.ConvertAsync(new AudioConversion(input, Path.Combine(dir, "out"), InputIsAac: true));

        Assert.Equal("/native/libffmpeg.so", runner.FileName);
        Assert.Equal(Path.Combine(dir, "out.m4a"), output);
        Assert.False(File.Exists(input));
        Assert.Equal("converted", await File.ReadAllTextAsync(output));
    }

    [Fact]
    public async Task Ffmpeg_NonZeroExit_Throws_AndKeepsInput()
    {
        var input = Input();
        var converter = new FfmpegAudioConverter(new FakeLocator("ffmpeg"), new FakeRunner(_ => new ProcessResult(1, "bad data\n")), new PassThroughAudioConverter());

        var error = await Assert.ThrowsAsync<AudioConversionException>(() =>
            converter.ConvertAsync(new AudioConversion(input, Path.Combine(dir, "out"), false)));

        Assert.Contains("bad data", error.Message);
        Assert.True(File.Exists(input));
    }

    [Fact]
    public async Task Ffmpeg_CannotStart_ThrowsConversionException()
    {
        var input = Input();
        var runner = new FakeRunner(_ => throw new Win32Exception("Permission denied"));
        var converter = new FfmpegAudioConverter(new FakeLocator("ffmpeg"), runner, new PassThroughAudioConverter());

        await Assert.ThrowsAsync<AudioConversionException>(() =>
            converter.ConvertAsync(new AudioConversion(input, Path.Combine(dir, "out"), false)));
    }

    [Fact]
    public async Task Ffmpeg_WithoutExecutable_FallsBackToPassThrough()
    {
        var input = Input("in.mp4");
        var runner = new FakeRunner(_ => throw new InvalidOperationException("must not run"));
        var converter = new FfmpegAudioConverter(new FakeLocator(null), runner, new PassThroughAudioConverter());

        var output = await converter.ConvertAsync(new AudioConversion(input, Path.Combine(dir, "out"), true));

        Assert.Equal(Path.Combine(dir, "out.mp4"), output);
        Assert.True(File.Exists(output));
    }

    [Fact]
    public async Task SystemProcessRunner_CapturesExitCodeAndErrors()
    {
        var shell = OperatingSystem.IsWindows() ? "cmd" : "sh";
        string[] arguments = OperatingSystem.IsWindows() ? ["/c", "echo oops 1>&2 & exit 3"] : ["-c", "echo oops >&2; exit 3"];

        var result = await new SystemProcessRunner().RunAsync(shell, arguments);

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("oops", result.ErrorOutput);
    }
}
