using System;
using System.Collections;
using System.Collections.Generic;
using MonoTorrent;
using Soddi.Services;
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

            return _bits.Length >= width ? SmushTheBitsRender(context, width) : ExpandTheBitsRender(context, width);
        }

        private IEnumerable<Segment> ExpandTheBitsRender(RenderContext context, int maxWidth)
        {
            var dotsPerSegment = (maxWidth * 8) / _bits.Length;
            var segment = new bool[8];
            var segmentPos = 0;

            foreach (var bit in _bits)
            {
                for (var i = 0; i < dotsPerSegment; i++)
                {
                    segment[segmentPos] = bit;
                    segmentPos++;

                    if (segmentPos == 8)
                    {
                        yield return RenderChunk(segment);

                        segmentPos = 0;
                    }
                }
            }
        }

        private IEnumerable<Segment> SmushTheBitsRender(RenderContext context, int maxWidth)
        {
            var bitsPerSegment = _bits.Length / (maxWidth * 8);
            if (bitsPerSegment == 0)
                bitsPerSegment = 1;
            
            var segment = new bool[8];
            var segmentPos = 0;
            var bitPos = 0;
            while (bitPos < _bits.Length)
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

                    yield return RenderChunk(segment);
                }
            }
        }

        private Segment RenderChunk(bool[] chunk)
        {
            return new(DotPattern.Get(new BitArray(chunk)).ToString());
        }
    }
}
