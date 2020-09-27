using System;
using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;

namespace Soddi
{
    /// <summary>
    /// Custom sentence builder for CommandLineParser to use mark down in
    /// the output
    /// </summary>
    internal class MarkedUpSentenceBuilder : SentenceBuilder
    {
        /// <summary>
        /// Creates an instance of MarkedUpSentenceBuilder
        /// </summary>
        /// <param name="regularSentenceBuilder">An instance of the default sentence builder. This is important
        /// because we'll use it to build the more complex help text. It must be instantiated prior to building this object
        /// because this object will overwrite the Factory
        /// </param>
        public MarkedUpSentenceBuilder(SentenceBuilder regularSentenceBuilder)
        {
            FormatMutuallyExclusiveSetErrors =
                errors => regularSentenceBuilder.FormatMutuallyExclusiveSetErrors(errors);
            FormatError = error => regularSentenceBuilder.FormatError(error);
            VersionCommandText = b => regularSentenceBuilder.VersionCommandText(b);
            HelpCommandText = b => regularSentenceBuilder.HelpCommandText(b);
        }

        public override Func<string> RequiredWord { get; } = () => "[white]Required.[/]";
        public override Func<string> OptionGroupWord { get; } = () => "[white]GROUP[/]";
        public override Func<string> ErrorsHeadingText { get; } = () => "[red]ERRORS[/]";
        public override Func<string> UsageHeadingText { get; } = () => "[white]USAGE[/]";
        public override Func<bool, string> HelpCommandText { get; }
        public override Func<bool, string> VersionCommandText { get; }
        public override Func<Error, string> FormatError { get; }

        public override Func<IEnumerable<MutuallyExclusiveSetError>, string> FormatMutuallyExclusiveSetErrors { get; }
    }
}
