using Spectre.Console.Rendering;

namespace Soddi.ProgressBar;

public sealed class FixedTaskDescriptionColumn : ProgressColumn
{
    private readonly int _width;

    public FixedTaskDescriptionColumn(int width)
    {
        _width = width;
    }

    public override IRenderable Render(RenderContext context, ProgressTask task, TimeSpan deltaTime)
    {
        var text = task.Description.Trim().PadRight(_width);
        return new Markup(text).Overflow(Overflow.Ellipsis).LeftAligned();
    }
}