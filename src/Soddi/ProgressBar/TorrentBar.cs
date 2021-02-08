using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MonoTorrent;
using Soddi.Services;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Soddi.ProgressBar
{
    internal sealed class TorrentBar : Renderable
    {
        public int? Width { get; set; }

        private readonly BitField _bits;

        public TorrentBar(BitSmuggler bitSmuggler)
        {
            _bits = bitSmuggler.Bits;
        }

        protected override Measurement Measure(RenderContext context, int maxWidth)
        {
            var width = Math.Min(Width ?? maxWidth, maxWidth);
            return new Measurement(4, width);
        }

        protected override IEnumerable<Segment> Render(RenderContext context, int maxWidth)
        {
            var width = Math.Min(Width ?? maxWidth, maxWidth);

            IEnumerable<Segment> segments;
            if (_bits.Length >= width)
            {
                segments = SmushTheBitsRender(context, width);
            }
            else
            {
                segments = ExpandTheBitsRender(context, width);
            }

            var list = segments.ToList();
            return list;
        }

        private IEnumerable<Segment> ExpandTheBitsRender(RenderContext context, int maxWidth)
        {
            var dotsPerSegment = (maxWidth * 8) / _bits.Length;
            var segment = new bool[8];
            var segmentPos = 0;


            var segmentsRendered = 0;
            foreach (var bit in _bits)
            {
                for (var i = 0; i < dotsPerSegment; i++)
                {
                    segment[segmentPos] = bit;
                    segmentPos++;

                    if (segmentPos == 8)
                    {
                        segmentsRendered++;
                        segmentPos = 0;
                        yield return RenderChunk(segment, context.LegacyConsole);
                    }
                }
            }

            if (segmentsRendered < maxWidth)
            {
                for (var i = segmentsRendered; i < maxWidth; i++)
                {
                    yield return RenderChunk(segment, context.LegacyConsole);
                }
            }
        }

        private IEnumerable<Segment> SmushTheBitsRender(RenderContext context, int maxWidth)
        {
            var bitsPerSegment = (int)Math.Round((decimal)_bits.Length / 8 / maxWidth);
            if (bitsPerSegment == 0)
                bitsPerSegment = 1;

            var segment = new bool[8];
            var segmentPos = 0;
            var bitPos = 0;
            var segmentsRendered = 0;
            while (bitPos + bitsPerSegment <= _bits.Length)
            {
                var bitAverage = 0;
                for (var i = 0; i < bitsPerSegment; i++)
                {
                    bitAverage += _bits[bitPos] ? 1 : 0;
                    bitPos++;
                }

                var thisBit = bitAverage >= 0.1;
                segment[segmentPos] = thisBit;
                segmentPos++;
                if (segmentPos == 8)
                {
                    segmentPos = 0;
                    segmentsRendered++;
                    yield return RenderChunk(segment, context.LegacyConsole);
                }
            }

            if (segmentsRendered < maxWidth)
            {
                for (var i = segmentsRendered; i < maxWidth; i++)
                {
                    yield return RenderChunk(segment, context.LegacyConsole);
                }
            }
        }

        private Segment RenderChunk(bool[] chunk, bool legacyConsole)
        {
            var style = Style.Plain;
            if (chunk.All(i => i == true))
            {
                style = new Style(foreground: Color.Green);
            }

            var segment = new Segment(DotPattern.Get(new BitArray(chunk)).ToString(), style);
            return segment;
        }
    }
}
