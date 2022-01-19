using System.Globalization;
using Humanizer.Bytes;
using Spectre.Console.Rendering;

namespace Soddi.ProgressBar;

/// <summary>
/// A column showing download progress.
/// </summary>
public sealed class DownloadedColumnExtended : ProgressColumn
{
    /// <summary>
    /// Gets or sets the <see cref="CultureInfo"/> to use.
    /// </summary>
    public CultureInfo? Culture { get; set; }

    /// <inheritdoc/>
    public override IRenderable Render(RenderContext context, ProgressTask task, TimeSpan deltaTime)
    {
        var total = ByteSize.FromBytes(task.MaxValue);
        var cultureInfo = Culture ?? CultureInfo.CurrentCulture;

        if (task.IsFinished)
        {
            return new Markup($"[green]{total.ToString("#.#", cultureInfo)}[/]");
        }
        else
        {
            var downloaded = ByteSize.FromBytes(task.Value);
            double remainingValue;
            switch (total.LargestWholeNumberSymbol)
            {
                case "b":
                    remainingValue = downloaded.Bits;
                    break;
                case "B":
                    remainingValue = downloaded.Bytes;
                    break;
                case "KB":
                    remainingValue = downloaded.Kilobytes;
                    break;
                case "MB":
                    remainingValue = downloaded.Megabytes;
                    break;
                case "GB":
                    remainingValue = downloaded.Gigabytes;
                    break;
                default:
                    remainingValue = downloaded.Terabytes;
                    break;
            }

            return new Markup(string.Format("{0:0.0}[grey]/[/]{1:0.0} [grey]{2}[/]", remainingValue,
                total.LargestWholeNumberValue, total.LargestWholeNumberSymbol));
        }
    }

    public override int? GetColumnWidth(RenderContext context)
    {
        return 16;
    }
}