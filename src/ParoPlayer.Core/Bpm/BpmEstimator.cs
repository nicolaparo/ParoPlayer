namespace ParoPlayer.Core.Bpm;

/// <summary>Estimates the tempo of mono PCM audio: energy onsets, autocorrelated over the plausible tempo range.</summary>
public static class BpmEstimator
{
    public const int MinBpm = 60;
    public const int MaxBpm = 200;

    private const int FramesPerSecond = 200;
    private const double MinConfidence = 0.1;

    /// <summary>Returns the tempo rounded to whole BPM, or null when there is no clear pulse (silence, too short, ambient).</summary>
    public static int? Estimate(ReadOnlySpan<float> samples, int sampleRate)
    {
        var onsets = Onsets(samples, sampleRate);
        var minLag = FramesPerSecond * 60.0 / MaxBpm;
        if (onsets.Length < minLag * 8)
        {
            return null;
        }

        var variance = Autocorrelation(onsets, 0);
        if (variance <= 0)
        {
            return null;
        }

        var best = 0.0;
        var bestBpm = 0.0;
        for (var bpm = (double)MinBpm; bpm <= MaxBpm; bpm += 0.25)
        {
            var lag = FramesPerSecond * 60.0 / bpm;
            var score = (Autocorrelation(onsets, lag) + Autocorrelation(onsets, lag * 2)) / variance * Preference(bpm);
            if (score > best)
            {
                best = score;
                bestBpm = bpm;
            }
        }

        return best >= MinConfidence ? (int)Math.Round(bestBpm) : null;
    }

    /// <summary>Softly favours tempos near 120 so that 70 and 140 do not tie.</summary>
    private static double Preference(double bpm)
    {
        var octaves = Math.Log2(bpm / 120);
        return Math.Exp(-0.5 * octaves * octaves);
    }

    /// <summary>Positive energy rises per frame, with the mean removed.</summary>
    private static double[] Onsets(ReadOnlySpan<float> samples, int sampleRate)
    {
        var hop = Math.Max(sampleRate / FramesPerSecond, 1);
        var frames = samples.Length / hop;
        var onsets = new double[Math.Max(frames, 0)];
        var previous = 0.0;
        for (var frame = 0; frame < frames; frame++)
        {
            var energy = 0.0;
            foreach (var sample in samples.Slice(frame * hop, hop))
            {
                energy += sample * sample;
            }

            var level = Math.Log(1 + 1000 * energy / hop);
            onsets[frame] = Math.Max(level - previous, 0);
            previous = level;
        }

        if (onsets.Length > 0)
        {
            var mean = onsets.Average();
            for (var i = 0; i < onsets.Length; i++)
            {
                onsets[i] -= mean;
            }
        }

        return onsets;
    }

    /// <summary>Mean product of the signal with itself shifted by a (fractional) lag.</summary>
    private static double Autocorrelation(double[] x, double lag)
    {
        var whole = (int)lag;
        var fraction = lag - whole;
        var count = x.Length - whole - 1;
        if (count <= 0)
        {
            return 0;
        }

        var sum = 0.0;
        for (var i = 0; i < count; i++)
        {
            sum += x[i] * (x[i + whole] * (1 - fraction) + x[i + whole + 1] * fraction);
        }

        return sum / count;
    }
}
