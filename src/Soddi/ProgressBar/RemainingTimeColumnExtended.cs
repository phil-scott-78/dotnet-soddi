using System;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Soddi.ProgressBar
{
    /// <summary>
    /// A column showing the remaining time of a task.
    /// </summary>
    public sealed class RemainingTimeColumnExtended : ProgressColumn
    {
        /// <inheritdoc/>
        protected override bool NoWrap => true;

        /// <summary>
        /// Gets or sets the style of the remaining time text.
        /// </summary>
        public Style Style { get; set; } = new Style(foreground: Color.Blue);

        public Func<TimeSpan, string> RemainingFormat = DefaultFormatter;

        private static string DefaultFormatter(TimeSpan t)
        {
            return $"{t:hh\\:mm\\:ss}";
        }

        public int? MaxWidth = 8;

        /// <inheritdoc/>
        public override IRenderable Render(RenderContext context, ProgressTask task, TimeSpan deltaTime)
        {
            var remaining = task.RemainingTime;
            if (remaining == null)
            {
                return new Markup("--:--:--");
            }

            return new Text(RemainingFormat(remaining.Value), Style ?? Style.Plain);
        }

        /// <inheritdoc/>
        public override int? GetColumnWidth(RenderContext context)
        {
            return MaxWidth ?? 8;
        }
    }
}
