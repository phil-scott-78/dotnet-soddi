using System.Globalization;

namespace Soddi;

public abstract class BaseLoggingOptions : CommandSettings
{
    [CommandOption("-l|--logLevel")]
    [Description("Minimum level for logging")]
    [TypeConverter(typeof(VerbosityConverter))]
    [DefaultValue(LogLevel.Information)]
    public LogLevel LogLevel { get; set; }
}

public sealed class VerbosityConverter : TypeConverter
{
    private readonly Dictionary<string, LogLevel> _lookup = new(StringComparer.OrdinalIgnoreCase)
    {
        { "o", LogLevel.Off },
        { "t", LogLevel.Trace },
        { "i", LogLevel.Information },
        { "w", LogLevel.Warning },
        { "e", LogLevel.Error },
        { "c", LogLevel.Critical }
    };

    public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string stringValue)
        {
            throw new NotSupportedException("Can't convert value to verbosity.");
        }

        var result = _lookup.TryGetValue(stringValue, out var verbosity);
        if (result)
        {
            return verbosity;
        }

        var message = string.Format(CultureInfo.InvariantCulture, "The value '{0}' is not a valid verbosity.", value);
        throw new InvalidOperationException(message);
    }
}
