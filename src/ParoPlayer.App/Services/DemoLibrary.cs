using ParoPlayer.Core.Models;
using ParoPlayer.Core.Services;
using ParoPlayer.Core.Storage;

namespace ParoPlayer.App.Services;

/// <summary>
/// Sample tracks for manual testing in DEBUG builds. Every track points at one of three generated sine-tone WAV files
/// in the library folder, so playback, notification and screen-off behaviour can be tried without importing music.
/// </summary>
public class DemoLibrary(LibraryService library, PlaylistService playlists, ILibraryFolder folder)
{
    private const string TonePrefix = "demo-tone-";
    private const int ToneSeconds = 60;
    private const int SampleRate = 16000;
    private static readonly double[] ToneFrequencies = [220, 330, 440];

    private static readonly (string Title, string Artist, string Album, string Genre, int Year, int Plays)[] Songs =
    [
        ("Midnight City", "M83", "Hurry Up, We're Dreaming", "Synth-pop", 2011, 41),
        ("Wait", "M83", "Hurry Up, We're Dreaming", "Synth-pop", 2011, 12),
        ("Cosmic Love", "Florence + The Machine", "Lungs", "Indie", 2009, 28),
        ("Dog Days Are Over", "Florence + The Machine", "Lungs", "Indie", 2009, 33),
        ("Nightcall", "Kavinsky", "OutRun", "Synthwave", 2010, 52),
        ("Weightless", "Marconi Union", "Weightless", "Ambient", 2011, 9),
        ("An Ending (Ascent)", "Brian Eno", "Apollo", "Ambient", 1983, 15),
        ("Blue Monday", "New Order", "Power, Corruption & Lies", "New Wave", 1983, 22),
        ("Heroes", "David Bowie", "Heroes", "Rock", 1977, 18),
        ("Space Oddity", "David Bowie", "Space Oddity", "Rock", 1969, 6),
        ("Teardrop", "Massive Attack", "Mezzanine", "Trip hop", 1998, 24),
        ("Intro", "The xx", "xx", "Indie", 2009, 3),
    ];

    public async Task AddAsync()
    {
        if ((await library.GetAllAsync()).Any(track => Path.GetFileName(track.FilePath).StartsWith(TonePrefix, StringComparison.Ordinal)))
        {
            return;
        }

        await folder.EnsureCreatedAsync();
        var tones = new List<string>();
        foreach (var frequency in ToneFrequencies)
        {
            tones.Add(await EnsureToneAsync(frequency));
        }

        foreach (var (song, index) in Songs.Select((song, index) => (song, index)))
        {
            await library.AddTrackAsync(new Track
            {
                Title = song.Title,
                Artist = song.Artist,
                Album = song.Album,
                Genre = song.Genre,
                Year = song.Year,
                DurationSeconds = ToneSeconds,
                FilePath = tones[index % tones.Count],
                PlayCount = song.Plays,
            });
        }

        await playlists.CreateSmartAsync("Most played", new SmartPlaylistDefinition(
            MatchMode.All, new[] { new SmartRule(SmartField.PlayCount, SmartOperator.GreaterThan, "20") }, SmartSort.MostPlayed, 50));
        await playlists.CreateSmartAsync("Ambient", new SmartPlaylistDefinition(
            MatchMode.All, new[] { new SmartRule(SmartField.Genre, SmartOperator.Equals, "Ambient") }));
        await playlists.CreateManualAsync("Road trip");
    }

    private async Task<string> EnsureToneAsync(double frequency)
    {
        var path = Path.Combine(folder.Path, $"{TonePrefix}{frequency:0}hz.wav");
        if (!File.Exists(path))
        {
            await File.WriteAllBytesAsync(path, ToneWav(frequency));
        }

        return path;
    }

    /// <summary>16-bit mono PCM sine with a short fade in and out so it does not click.</summary>
    private static byte[] ToneWav(double frequency)
    {
        var samples = SampleRate * ToneSeconds;
        var data = new byte[samples * 2];
        for (var i = 0; i < samples; i++)
        {
            var t = i / (double)SampleRate;
            var fade = Math.Min(1.0, Math.Min(t, ToneSeconds - t) / 0.05);
            var value = (short)(Math.Sin(2 * Math.PI * frequency * t) * 0.25 * fade * short.MaxValue);
            BitConverter.TryWriteBytes(data.AsSpan(i * 2), value);
        }

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + data.Length);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(data.Length);
        writer.Write(data);
        writer.Flush();
        return stream.ToArray();
    }
}
