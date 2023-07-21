using System.Runtime.CompilerServices;
using System.Text;

namespace Soddi;

public enum LogLevel
{
    Off,
    Critical,
    Error,
    Warning,
    Information,
    Trace
}

class LogInterceptor : ICommandInterceptor
{
    public void Intercept(CommandContext context, CommandSettings settings)
    {
        if (settings is BaseLoggingOptions loggingOptions)
        {
            Log.EnabledLevel = loggingOptions.LogLevel;
        }
        else
        {
            Log.EnabledLevel = LogLevel.Error;
        }
    }
}

public class Logger
{
    public static readonly Logger Log = new();

    public LogLevel EnabledLevel { get; set; } = LogLevel.Error;

    public void Write(LogLevel level,
        [InterpolatedStringHandlerArgument("", "level")] LogInterpolatedStringHandler builder)
    {
        if (EnabledLevel < level) return;

        var message = $"[[{level,10}]]: {builder.GetFormattedText()}";
        AnsiConsole.MarkupLine(message);
    }
}

[InterpolatedStringHandler]
public ref struct LogInterpolatedStringHandler
{
    // Storage for the built-up string
    private readonly StringBuilder? _builder;

    public LogInterpolatedStringHandler(int literalLength, int formattedCount, Logger logger, LogLevel logLevel)
    {
        var enabled = logger.EnabledLevel >= logLevel;
        if (enabled)
        {
            _builder = formattedCount == 0 
                ? new StringBuilder(literalLength) 
                : new StringBuilder(literalLength + formattedCount);
        }
        else
        {
            _builder = null;
        }
    }

    public void AppendLiteral(string s)
    {
        if (_builder == null) return;

        _builder.Append(s);
    }

    public void AppendFormatted<T>(T t)
    {
        if (_builder == null) return;

        _builder.Append("[blue]");
        _builder.Append(t);
        _builder.Append("[/]");
    }

    internal string GetFormattedText() => _builder?.ToString() ?? string.Empty;
}
