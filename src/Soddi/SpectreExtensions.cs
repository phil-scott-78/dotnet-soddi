using System.Collections.Generic;
using Spectre.Console.Cli;

namespace Soddi
{
    public static class SpectreExtensions
    {
        public static IConfigurator AddCommandWithExample<T>(
            this IConfigurator configurator,
            string name,
            string description,
            IEnumerable<string[]> examples,
            bool isHidden = false)
            where T : class, ICommand
        {
            var commandConfig = configurator
                .AddCommand<T>(name)
                .WithDescription(description);

            if (isHidden)
            {
                commandConfig = commandConfig.IsHidden();
            }

            commandConfig.WithExamples(configurator, examples, isHidden);

            return configurator;
        }

        public static ICommandConfigurator WithExamples(
            this ICommandConfigurator command,
            IConfigurator configurator,
            IEnumerable<string[]> examples,
            bool isHidden = false
        )
        {
            var isFirst = true;
            foreach (var example in examples)
            {
                if (isFirst)
                {
                    if (isHidden == false)
                    {
                        configurator.AddExample(example);
                    }

                    isFirst = false;
                }

                command.WithExample(example);
            }

            return command;
        }
    }
}
