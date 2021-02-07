using System;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Soddi.ProgressBar
{
    internal sealed class TorrentProgressBarColumn : ProgressColumn
    {
        public int? Width { get; set; } = 40;

        public override IRenderable Render(RenderContext context, ProgressTask task, TimeSpan deltaTime)
        {
            var bits = task.State.Get<BitSmuggler>("torrentBits");
            return new TorrentBar(bits) { Width = Width };
        }
    }
}
