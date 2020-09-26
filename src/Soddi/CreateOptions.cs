using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using JetBrains.Annotations;
using MediatR;
using Soddi.Services;
using Soddi.Tasks;
using Soddi.Tasks.SqlServer;

namespace Soddi
{
    [Verb("create", HelpText = "Create a new database from a 7z archive or folder"), UsedImplicitly]
    public class CreateOptions : IRequest<int>
    {
        public CreateOptions(string path, string databaseName, string connectionString, bool dropAndRecreate,
            bool primaryKeys)
        {
            Path = path;
            DatabaseName = databaseName;
            ConnectionString = connectionString;
            DropAndRecreate = dropAndRecreate;
            PrimaryKeys = primaryKeys;
        }

        [Value(0, Required = true, MetaName = "Path", HelpText = "File or folder containing xml archives")]
        public string Path { get; }

        [Option('d', "database", HelpText = "Database name to create. Will be file name by default")]
        public string DatabaseName { get; }

        [Option('c', "connectionString", HelpText = "Connection string to server",
            Default = "Server=.;Integrated Security=true")]
        public string ConnectionString { get; }

        [Option("dropAndCreate", HelpText = "Drop and recreate database in default file location",
            Default = false)]
        public bool DropAndRecreate { get; }

        [Option('p', "primaryKeys", HelpText = "Add primary keys and unique constraints", Default = true)]
        public bool PrimaryKeys { get; }
    }

    public class CreateHandler : IRequestHandler<CreateOptions, int>
    {
        private readonly DatabaseHelpers _databaseHelpers;

        public CreateHandler(DatabaseHelpers databaseHelpers)
        {
            _databaseHelpers = databaseHelpers;
        }

        public Task<int> Handle(CreateOptions request, CancellationToken cancellationToken)
        {
            var dbName = _databaseHelpers.GetDbNameFromPathOption(request.DatabaseName, request.Path);
            var tasks = new Queue<(string name, ITask task)>();
            var (masterConnectionString, databaseConnectionString) = _databaseHelpers.GetMasterAndDbConnectionStrings(request.ConnectionString, dbName);

            var processor = _databaseHelpers.VerifyAndCreateProcessor(request.Path);

            if (request.DropAndRecreate)
            {
                tasks.Enqueue(("Create new database", new CreateDatabase(masterConnectionString, dbName)));
            }

            tasks.Enqueue(("Create schema", new CreateSchema(databaseConnectionString)));
            if (request.PrimaryKeys)
            {
                tasks.Enqueue(("Add constraints", new AddConstraints(databaseConnectionString)));
            }

            tasks.Enqueue(("Insert type values", new InsertTypeValues(databaseConnectionString)));
            tasks.Enqueue(("Insert data from archive", new InsertData(databaseConnectionString, dbName, processor)));

            var maxTicks = tasks.Sum(i => i.task.GetTaskWeight());
            var progressBar = new FudgedProgressBar(maxTicks, "Running", ConsoleColor.Blue);

            foreach (var (_, task) in tasks)
            {
                var progress = new Progress<(string message, int weight)>(i =>
                {
                    var (message, weight) = i;
                    progressBar.Tick(weight, message);
                });

                task.Go(progress);
                progressBar.AddTaskWeight(task.GetTaskWeight());
            }

            progressBar.WrapUp("Done");
            return Task.FromResult(0);
        }
    }
}
