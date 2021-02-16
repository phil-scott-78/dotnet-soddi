using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.ExceptionServices;
using MonoTorrent;

namespace Soddi.ProgressBar
{
    internal readonly struct BitSmuggler
    {
        public BitSmuggler(BitField bits)
        {
            Bits = bits;
        }

        public BitField Bits { get; }

        public IEnumerable<decimal> Smush(int desiredLength)
        {
            return BitAverage.Average(Bits, desiredLength);
        }
    }

    public static class BitAverage
    {
        public static IImmutableList<decimal> Average(IEnumerable<bool> bitsEnumerable, int desiredLength)
        {
            var bits = bitsEnumerable.Select(i => i ? 1m : 0m).ToImmutableList();
            if (bits.Count == desiredLength)
            {
                return bits;
            }

            if (bits.Count > desiredLength && bits.Count < desiredLength * 2)
            {
                bits = ExpandTheBits(bits, desiredLength * 10).ToImmutableList();
            }

            if (bits.Count > desiredLength)
            {
                bits = SmushTheBits(bits, desiredLength).ToImmutableList();
            }
            else if (bits.Count <= desiredLength / 2)
            {
                bits = ExpandTheBits(bits, desiredLength).ToImmutableList();
            }

            return PadTheBits(bits, desiredLength).ToImmutableList();
        }

        private static IEnumerable<decimal> ExpandTheBits(ImmutableList<decimal> bits, int desiredLength)
        {
            var expandedBitsPerInputBit = Convert.ToInt16(Math.Floor((decimal)(desiredLength) / bits.Count));
            var bitCount = 0;
            var lastBit = 0m;
            foreach (var bit in bits)
            {
                for (var i = 0; i < expandedBitsPerInputBit; i++)
                {
                    lastBit = bit;
                    bitCount++;

                    yield return bit;
                }
            }

            for (var i = bitCount; i < desiredLength; i++)
            {
                yield return lastBit;
            }
        }

        private static IEnumerable<decimal> SmushTheBits(ImmutableList<decimal> bits, int desiredLength)
        {
            var inputBitsPerReturnBit = Convert.ToInt16(Math.Floor((decimal)bits.Count / desiredLength));
            var pos = 0;
            var chunkTotal = 0m;
            var chunkCount = 0;
            var bitCount = 0;

            while (pos < bits.Count)
            {
                chunkTotal += bits[pos];
                chunkCount++;
                pos++;

                if (chunkCount == inputBitsPerReturnBit)
                {
                    yield return chunkTotal / inputBitsPerReturnBit;

                    chunkTotal = 0;
                    chunkCount = 0;
                    bitCount++;
                    if (bitCount > desiredLength)
                    {
                        yield break;
                    }
                }
            }

            if (chunkCount != 0)
            {
                yield return chunkTotal / inputBitsPerReturnBit;
            }
        }

        private static IEnumerable<decimal> PadTheBits(ImmutableList<decimal> bits, int desiredLength)
        {
            var numberOfDoubleUps = (desiredLength - bits.Count) / 2;
            var totalRendered = 0;
            for (var index = 0; index < bits.Count; index++)
            {
                if (index < numberOfDoubleUps || index >= bits.Count - numberOfDoubleUps)
                {
                    totalRendered++;
                    yield return bits[index];
                }

                totalRendered++;
                yield return bits[index];
            }

            for (var index = totalRendered; index < desiredLength; index++)
            {
                yield return bits[^1];
            }
        }
    }
}
