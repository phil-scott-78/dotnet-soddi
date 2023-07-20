using Spectre.Console.Rendering;

namespace Soddi.ProgressBar;

public sealed class FixedTaskDescriptionColumn(int width) : ProgressColumn
{
    public override IRenderable Render(RenderContext context, ProgressTask task, TimeSpan deltaTime)
    {
        var text = task.Description.Trim().PadRight(width);
        return new Markup(text).Overflow(Overflow.Ellipsis).LeftAligned();
    }
}