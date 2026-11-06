namespace ParoPlayer.Core.Harmony;

/// <summary>
/// Estimates the musical key of mono PCM audio: spectrum frames folded into a 12-bin chroma vector, averaged over time
/// and correlated with the Krumhansl-Schmuckler major and minor profiles in all 12 rotations.
/// </summary>
public static class KeyEstimator
{
    private const double MinHz = 100;
    private const double MaxHz = 2000;
    private const double FrameSeconds = 0.3;
    private const double SilenceEnergy = 1e-6;

    private static readonly string[] Names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
    private static readonly double[] Major = [6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88];
    private static readonly double[] Minor = [6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17];

    /// <summary>Returns the key such as "C" or "C#m", or null when there is no tonal content (silence, too short).</summary>
    public static string? Estimate(ReadOnlySpan<float> samples, int sampleRate)
    {
        var chroma = Chroma(samples, sampleRate);
        if (chroma.Sum() <= 0)
        {
            return null;
        }

        var best = double.MinValue;
        var bestName = (string?)null;
        for (var tonic = 0; tonic < 12; tonic++)
        {
            var major = Correlation(chroma, Major, tonic);
            if (major > best)
            {
                best = major;
                bestName = Names[tonic];
            }

            var minor = Correlation(chroma, Minor, tonic);
            if (minor > best)
            {
                best = minor;
                bestName = Names[tonic] + "m";
            }
        }

        return bestName;
    }

    /// <summary>Spectral magnitude per pitch class (C = 0), summed over Hann-windowed frames.</summary>
    private static double[] Chroma(ReadOnlySpan<float> samples, int sampleRate)
    {
        var size = 1024;
        while (size < sampleRate * FrameSeconds)
        {
            size *= 2;
        }

        var chroma = new double[12];
        if (samples.Length < size)
        {
            return chroma;
        }

        var window = new double[size];
        for (var i = 0; i < size; i++)
        {
            window[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (size - 1));
        }

        var pitchClass = new int[size / 2];
        for (var bin = 1; bin < pitchClass.Length; bin++)
        {
            var hz = (double)bin * sampleRate / size;
            var semitones = (int)Math.Round(12 * Math.Log2(hz / 440)) + 9;
            pitchClass[bin] = hz is >= MinHz and <= MaxHz ? ((semitones % 12) + 12) % 12 : -1;
        }

        var real = new double[size];
        var imaginary = new double[size];
        for (var start = 0; start + size <= samples.Length; start += size)
        {
            var energy = 0.0;
            for (var i = 0; i < size; i++)
            {
                var sample = samples[start + i];
                energy += sample * sample;
                real[i] = sample * window[i];
                imaginary[i] = 0;
            }

            if (energy / size < SilenceEnergy)
            {
                continue;
            }

            Fft(real, imaginary);
            for (var bin = 1; bin < pitchClass.Length; bin++)
            {
                if (pitchClass[bin] >= 0)
                {
                    chroma[pitchClass[bin]] += Math.Sqrt(real[bin] * real[bin] + imaginary[bin] * imaginary[bin]);
                }
            }
        }

        return chroma;
    }

    /// <summary>Pearson correlation of the chroma with a profile whose tonic sits at pitch class <paramref name="tonic"/>.</summary>
    private static double Correlation(double[] chroma, double[] profile, int tonic)
    {
        var chromaMean = chroma.Average();
        var profileMean = profile.Average();
        double covariance = 0, chromaVariance = 0, profileVariance = 0;
        for (var i = 0; i < 12; i++)
        {
            var c = chroma[(i + tonic) % 12] - chromaMean;
            var p = profile[i] - profileMean;
            covariance += c * p;
            chromaVariance += c * c;
            profileVariance += p * p;
        }

        var divisor = Math.Sqrt(chromaVariance * profileVariance);
        return divisor == 0 ? 0 : covariance / divisor;
    }

    /// <summary>In-place iterative radix-2 FFT; the length must be a power of two.</summary>
    private static void Fft(double[] real, double[] imaginary)
    {
        var n = real.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            var bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1)
            {
                j ^= bit;
            }

            j ^= bit;
            if (i < j)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imaginary[i], imaginary[j]) = (imaginary[j], imaginary[i]);
            }
        }

        for (var length = 2; length <= n; length <<= 1)
        {
            var angle = -2 * Math.PI / length;
            var stepReal = Math.Cos(angle);
            var stepImaginary = Math.Sin(angle);
            for (var start = 0; start < n; start += length)
            {
                double wReal = 1, wImaginary = 0;
                for (var k = 0; k < length / 2; k++)
                {
                    var a = start + k;
                    var b = a + length / 2;
                    var tReal = real[b] * wReal - imaginary[b] * wImaginary;
                    var tImaginary = real[b] * wImaginary + imaginary[b] * wReal;
                    real[b] = real[a] - tReal;
                    imaginary[b] = imaginary[a] - tImaginary;
                    real[a] += tReal;
                    imaginary[a] += tImaginary;
                    (wReal, wImaginary) = (wReal * stepReal - wImaginary * stepImaginary, wReal * stepImaginary + wImaginary * stepReal);
                }
            }
        }
    }
}
