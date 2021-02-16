using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Soddi.ProgressBar;
using Xunit;

namespace Soddi.Tests
{
    public class BitAveragerTests
    {
        [Theory]
        [MemberData(nameof(Data))]
        public void Can_smush(int size, bool[] input, decimal[] expected)
        {
            var result = BitAverage.Average(input, size).ToArray();
            result.ShouldBe(expected);
        }

        public static IEnumerable<object[]> Data()
        {
            yield return new object[]
            {
                320, FilledArray(165, false).Concat(FilledArray(165, true)).ToArray(),
                FilledArray(160, 0m).Concat(FilledArray(160, 1m)).ToArray()
            };
            
            yield return new object[]
            {
                320, FilledArray(150, false).Concat(FilledArray(150, true)).ToArray(),
                FilledArray(160, 0m).Concat(FilledArray(160, 1m)).ToArray()
            };

            yield return new object[]
            {
                4, new[] { true, true, true, true, false, false, false, false }, new[] { 1m, 1m, 0m, 0m }
            };
            yield return new object[]
            {
                4, new[] { true, false, false, true, false, false, false, false }, new[] { .5m, .5m, 0m, 0m }
            };
            yield return new object[] { 3, new[] { true, false }, new decimal[] { 1, 0, 0 } };
            yield return new object[] { 5, new[] { true, false }, new decimal[] { 1, 1, 0, 0, 0 } };
            yield return new object[] { 4, new[] { true, false }, new decimal[] { 1, 1, 0, 0 } };
            yield return new object[] { 6, new[] { true, false }, new decimal[] { 1, 1, 1, 0, 0, 0 } };
        }

        private static T[] FilledArray<T>(int count, T value)
        {
            var data = new T[count];
            data.AsSpan().Fill(value);
            return data;
        }
    }
}
