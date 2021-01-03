using System;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using JetBrains.Annotations;
using Lamar;
using MediatR;
using Spectre.Console;

namespace Soddi
{
    [UsedImplicitly]
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
#if DEBUG
            //args = new[] { "import", @"C:\Users\phils\Downloads\aviation.stackexchange.com\", "--dropAndCreate" };
            // args = new[] { "list" };
            // args = new[] {"download", "space"};
            // args = new[] { "torrent", "math" };
            args = new[] { "import", @"space.stackexchange.com.7z", "--dropAndCreate", "-d", "space" };
            // args = new string[] {"help", "torrent"};
#endif

            // find all classes that implement IRequest<int>. They are the verbs
            // that the application supports
            var commands = Assembly
                .GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.GetInterfaces().Contains(typeof(IRequest<int>)))
                .ToArray();

            // parse the command lines and cast the parsed command line argument
            // back into IRequest<int>.
            var parser = new Parser();
            var parserResult = parser.ParseArguments(args, commands);

            if (!(parserResult is Parsed<object> parsed))
            {
                // couldn't parse the command line, display help and exit
                var helpText = HelpText.AutoBuild(
                    parserResult,
                    text => GetHelpText(parserResult, text),
                    AnsiConsole.Width);

                AnsiConsole.MarkupLine(helpText.ToString());
                return 1;
            }

            using var container = BuildContainer();
            var mediator = container.GetInstance<IMediator>();

            try
            {
                // send the request to the appropriate handler
                var request = (IRequest<int>)parsed.Value;
                return await mediator.Send(request);
            }
            catch (SoddiException e)
            {
                Console.WriteLine(e.Message);
                return 1;
            }
        }

        private static HelpText GetHelpText(ParserResult<object> parserResult, HelpText text)
        {
            var regularSentenceBuilder = SentenceBuilder.Create();
            SentenceBuilder.Factory = () => new MarkedUpSentenceBuilder(regularSentenceBuilder);

            var notParsed = (NotParsed<object>)parserResult;

            var helpVerb = notParsed.Errors.OfType<HelpVerbRequestedError>().FirstOrDefault();
            var verb = helpVerb?.Type?.GetCustomAttributes().OfType<VerbAttribute>().FirstOrDefault();
            if (verb != null && !string.IsNullOrWhiteSpace(verb.HelpText))
            {
                text.Heading = verb.HelpText;
                text.Copyright = string.Empty;
            }
            else
            {
                text.Heading = "Download and import Stack Overflow archive data.";
                text.Copyright = "";
            }

            text.AdditionalNewLineAfterOption = false;
            text.AddNewLineBetweenHelpSections = true;

            return text;
        }

        private static Container BuildContainer()
        {
            return new Container(cfg =>
            {
                cfg.Scan(scanner =>
                {
                    scanner.AssemblyContainingType<Program>();
                    scanner.ConnectImplementationsToTypesClosing(typeof(IRequestHandler<,>));
                });

                cfg.For<IFileSystem>().Use<FileSystem>();
                cfg.For<IMediator>().Use<Mediator>().Transient();
                cfg.For<ServiceFactory>().Use(ctx => ctx.GetInstance);
            });
        }
    }

    public class SoddiException : Exception
    {
        public SoddiException(string? message) : base(message)
        {
        }
    }
}
