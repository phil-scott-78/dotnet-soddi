﻿using System;
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

            return ExpandTheBitsRender(context, width);
        }

        private IEnumerable<Segment> ExpandTheBitsRender(RenderContext context, int maxWidth)
        {
            var bits = BitAverage.Average(_bits, maxWidth * 8);
            var segmentPos = 0;
            decimal[] segment = new decimal[8];
            foreach (var bit in bits)
            {
                segment[segmentPos] = bit;
                segmentPos++;

                if (segmentPos == 8)
                {
                    yield return RenderChunk(segment, context.LegacyConsole);

                    segmentPos = 0;
                }
            }
        }


        private Segment RenderChunk(decimal[] chunk, bool legacyConsole)
        {
            var style = Style.Plain;
            if (chunk.All(i => i == 1m))
            {
                style = new Style(Color.Green);
            }

            var segment = new Segment(DotPattern.Get(new BitArray(chunk.Select(i => i > .6m).ToArray())).ToString(), style);
            return segment;
        }
    }
}
