using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using Lamar;
using MediatR;
using MediatR.Pipeline;

namespace Soddi
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
#if DEBUG
            // args = new[] {"create", @"C:\Users\phils\Downloads\aviation.stackexchange.com\", "--dropAndCreate"};
            // args = new[] {"list", "-p", "stack"};
            args = new[] {"download", "space"};
#endif

            var commands = Assembly
                .GetExecutingAssembly()
                .GetTypes()
                .Where(t => t.GetInterfaces().Contains(typeof(IRequest<int>)))
                .ToArray();

            var parserResult = Parser.Default.ParseArguments(args, commands);
            if (parserResult is Parsed<object> parser && parser.Value is IRequest<int> request)
            {
                var mediator = BuildContainer().GetInstance<IMediator>();
                try
                {
                    return await mediator.Send(request);
                }
                catch (SoddiException e)
                {
                    Console.WriteLine(e.Message);
                    return 1;
                }
            }

            Console.WriteLine(HelpText.AutoBuild(parserResult));
            return 1;
        }

        private static Container BuildContainer()
        {
            return new Container(cfg =>
            {
                cfg.Scan(scanner =>
                {
                    scanner.AssemblyContainingType<Program>();
                    scanner.ConnectImplementationsToTypesClosing(typeof(IRequestHandler<,>));
                    scanner.ConnectImplementationsToTypesClosing(typeof(INotificationHandler<>));
                });

                //Pipeline
                cfg.For(typeof(IPipelineBehavior<,>))
                    .Add(typeof(RequestPreProcessorBehavior<,>));
                cfg.For(typeof(IPipelineBehavior<,>))
                    .Add(typeof(RequestPostProcessorBehavior<,>));

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

        public SoddiException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
