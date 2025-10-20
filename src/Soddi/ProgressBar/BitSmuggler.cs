using System.Buffers;
using MonoTorrent;

namespace Soddi.ProgressBar;

internal readonly struct BitSmuggler(ReadOnlyBitField bits)
{
    public ReadOnlyBitField Bits { get; } = bits;

    public IEnumerable<decimal> Smush(int desiredLength)
    {
        return BitAverage.Average(Bits.ToBoolArray(), desiredLength);
    }
}

public static class BitAverage
{
    private static int GreatestCommonFactor(int a, int b)
    {
        while (b != 0)
        {
            var temp = b;
            b = a % b;
            a = temp;
        }

        return a;
    }

    private static int LeastCommonMultiple(int a, int b)
    {
        return a / GreatestCommonFactor(a, b) * b;
    }

    private static readonly ConcurrentDictionary<(int, int), int> s_leastCommonMultipleCache = new();
    private static readonly ArrayPool<decimal> s_decimalArrayPool = ArrayPool<decimal>.Shared;

    public static IEnumerable<decimal> Average(IList<bool> inputBits, int desiredLength)
    {
        if (inputBits.Count == desiredLength)
        {
            return inputBits.Select(i => i ? 1m : 0m).ToArray();
        }

        var lcm = s_leastCommonMultipleCache.GetOrAdd((inputBits.Count, desiredLength), value => LeastCommonMultiple(value.Item1, value.Item2));
        var buffer = s_decimalArrayPool.Rent(lcm);
        var outputBuffer = new decimal[desiredLength];

        ExpandTheBits(inputBits.Select(i => i ? 1m : 0m).ToList(), lcm, buffer);
        SmushTheBits(buffer, outputBuffer, lcm, desiredLength);

        s_decimalArrayPool.Return(buffer);

        return outputBuffer;
    }

    private static void ExpandTheBits(ICollection<decimal> bits, int desiredLength, decimal[] buffer)
    {
        var expandedBitsPerInputBit = Convert.ToInt16(Math.Floor((decimal)desiredLength / bits.Count));
        var bitCount = 0;
        var lastBit = 0m;
        foreach (var bit in bits)
        {
            for (var i = 0; i < expandedBitsPerInputBit; i++)
            {
                lastBit = bit;
                buffer[bitCount] = bit;
                bitCount++;
            }
        }

        for (var i = bitCount; i < desiredLength; i++)
        {
            buffer[i] = lastBit;
        }
    }

    private static void SmushTheBits(IReadOnlyList<decimal> bits, decimal[] outputBuffer, int bufferSize, int desiredLength)
    {
        var inputBitsPerReturnBit = Convert.ToInt16(Math.Floor((decimal)bufferSize / desiredLength));
        var pos = 0;
        var chunkTotal = 0m;
        var chunkCount = 0;
        var bitCount = 0;

        while (pos < bufferSize)
        {
            chunkTotal += bits[pos];
            chunkCount++;
            pos++;

            if (chunkCount != inputBitsPerReturnBit)
            {
                continue;
            }

            outputBuffer[bitCount] = chunkTotal / inputBitsPerReturnBit;

            chunkTotal = 0;
            chunkCount = 0;
            bitCount++;
            if (bitCount > desiredLength)
            {
                break;
            }
        }

        if (chunkCount != 0)
        {
            outputBuffer[bitCount] = chunkTotal / inputBitsPerReturnBit;
        }
    }
}
