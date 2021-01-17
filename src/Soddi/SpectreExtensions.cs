using System.Collections.Generic;
using Spectre.Console.Cli;

namespace Soddi
{
    public static class SpectreExtensions
    {
        public static IConfigurator AddCommandWithExample<T>(this IConfigurator configurator, string name, IEnumerable<string[]> examples)
            where T : class, ICommand
        {
            configurator.AddCommand<T>(name).WithExamples(configurator, examples);
            return configurator;
        }

        public static ICommandConfigurator WithExamples(
            this ICommandConfigurator command,
            IConfigurator configurator,
            IEnumerable<string[]> examples)
        {
            var isFirst = true;
            foreach (var example in examples)
            {
                if (isFirst)
                {
                    configurator.AddExample(example);
                    isFirst = false;
                }

                command.WithExample(example);
            }

            return command;
        }
    }
}
