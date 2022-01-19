using MonoTorrent;
using Soddi.Services;
using Spectre.Console.Rendering;

namespace Soddi.ProgressBar;

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

        return ExpandTheBitsRender(width);
    }

    private IEnumerable<Segment> ExpandTheBitsRender(int maxWidth)
    {
        var bits = BitAverage.Average(_bits, maxWidth * 8);
        var segmentPos = 0;
        var segment = new decimal[8];
        foreach (var bit in bits)
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


    private Segment RenderChunk(decimal[] chunk)
    {
        var style = Style.Plain;
        if (chunk.All(i => i == 1m))
        {
            style = new Style(Color.Green);
        }

        const decimal MinimumValueToConsiderDownloaded = .5m;
        if (chunk.All(i => i < MinimumValueToConsiderDownloaded) && chunk.Any(i => i > .1m))
        {
            // if we don't have any dots to display, but we do have a bit that is at least
            // partial then display one little dot in grey
            var first = true;
            return new Segment(DotPattern.Get(new BitArray(chunk.Select(i =>
            {
                if (i <= .1m || !first)
                {
                    return false;
                }

                first = false;
                return true;
            }).ToArray())).ToString(), new Style(Color.Grey));
        }

        var segment = new Segment(DotPattern.Get(new BitArray(chunk.Select(i => i > MinimumValueToConsiderDownloaded).ToArray())).ToString(), style);
        return segment;
    }
}
