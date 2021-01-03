﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using JetBrains.Annotations;
using MediatR;
using Soddi.Services;
using Soddi.Tasks;
using Soddi.Tasks.SqlServer;
using Spectre.Console;

namespace Soddi
{
    [Verb("import", HelpText = "Import data from a 7z archive or folder"), UsedImplicitly]
    public class ImportOptions : IRequest<int>
    {
        public ImportOptions(string path, string databaseName, string connectionString, bool dropAndRecreate,
            bool skipPrimaryKeys)
        {
            Path = path;
            DatabaseName = databaseName;
            ConnectionString = connectionString;
            DropAndRecreate = dropAndRecreate;
            SkipPrimaryKeys = skipPrimaryKeys;
        }

        [Value(0, Required = true, MetaName = "Path",
            HelpText =
                "File or folder containing xml archives. The file must be a .7z file. If using a folder it can contain either .7z or .xml content")]
        public string Path { get; }

        [Option('d', "database",
            HelpText = "Name of database. If omitted the name of the file or folder will be used.")]
        public string DatabaseName { get; }

        [Option('c', "connectionString",
            HelpText =
                "Connection string to server. Initial catalog will be ignored and the database parameter will be used for the database name.",
            Default = "Server=.;Integrated Security=true")]
        public string ConnectionString { get; }

        [Option("dropAndCreate",
            HelpText =
                "Drop and recreate database. If a database already exists with this name it will be dropped. Then a database will be created in the default server file location with the default server options.",
            Default = false)]
        public bool DropAndRecreate { get; }

        [Option("skipConstraints", HelpText = "Skip adding primary keys and unique constraints.", Default = false)]
        public bool SkipPrimaryKeys { get; }

        [Usage(ApplicationAlias = "soddi"), UsedImplicitly]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Import data using defaults",
                    new ImportOptions("math.stackexchange.com.7z", "", "", false, false));
                yield return new Example("Import data using a connection string and database name",
                    new ImportOptions("math.stackexchange.com.7z", "math",
                        "Server=(local)\\Sql2017;User Id=admin;password=t3ddy", false, false));
                yield return new Example("Import data using defaults and create database",
                    new ImportOptions("math.stackexchange.com.7z", "", "", true, false));
                yield return new Example("Import data using defaults without constraints",
                    new ImportOptions("math.stackexchange.com.7z", "", "", false, true));
            }
        }
    }


    public class ImportHandler : IRequestHandler<ImportOptions, int>
    {
        private readonly DatabaseHelper _databaseHelper;
        private readonly ProcessorFactory _processorFactory;

        public ImportHandler(DatabaseHelper databaseHelper, ProcessorFactory processorFactory)
        {
            _databaseHelper = databaseHelper;
            _processorFactory = processorFactory;
        }

        public Task<int> Handle(ImportOptions request, CancellationToken cancellationToken)
        {
            var dbName = _databaseHelper.GetDbNameFromPathOption(request.DatabaseName, request.Path);
            var tasks = new Queue<(string name, ITask task)>();
            var (masterConnectionString, databaseConnectionString) =
                _databaseHelper.GetMasterAndDbConnectionStrings(request.ConnectionString, dbName);

            var processor = _processorFactory.VerifyAndCreateProcessor(request.Path);

            if (request.DropAndRecreate)
            {
                tasks.Enqueue(("Create new database", new CreateDatabase(masterConnectionString, dbName)));
            }
            else
            {
                tasks.Enqueue(("Verify database exists", new VerifyDatabaseExists(masterConnectionString, dbName)));
            }

            tasks.Enqueue(("Create schema", new CreateSchema(databaseConnectionString)));
            if (!request.SkipPrimaryKeys)
            {
                tasks.Enqueue(("Add constraints", new AddConstraints(databaseConnectionString)));
            }

            tasks.Enqueue(("Insert type values", new InsertTypeValues(databaseConnectionString)));
            tasks.Enqueue(("Insert data from archive", new InsertData(databaseConnectionString, dbName, processor)));

            var progressBar = AnsiConsole.Progress()
                .AutoClear(false)
                .Columns(new ProgressColumn[]
                {
                    new FixedTaskDescriptionColumn(40), // Task description
                    new ProgressBarColumn(), // Progress bar
                    new PercentageColumn(), // Percentage
                    new RemainingTimeColumn(), // Remaining time
                    new SpinnerColumn(), // Spinner
                });

            progressBar.Start(ctx =>
            {
                foreach (var (description, task) in tasks)
                {
                    var progressBarTask = ctx.AddTask(description);
                    progressBarTask.MaxValue(task.GetTaskWeight());
                    var progress = new Progress<(string message, int weight)>(i =>
                    {
                        var (message, weight) = i;
                        progressBarTask.Increment(weight);
                        progressBarTask.Description(message);
                    });

                    task.Go(progress);
                    progressBarTask.Increment(progressBarTask.MaxValue - progressBarTask.Value);
                }
            });

            return Task.FromResult(0);
        }
    }
}
