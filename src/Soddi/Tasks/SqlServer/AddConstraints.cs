﻿using Microsoft.Data.SqlClient;

namespace Soddi.Tasks.SqlServer;

public class AddConstraints(string connectionString) : ITask
{
    public async Task GoAsync(IProgress<(string taskId, string message, double weight, double maxValue)> progress, CancellationToken cancellationToken)
    {
        progress.Report(("add-constraints", "Adding constraints", 0, GetTaskWeight()));
        var statements = Sql.Split("GO");
        await using var sqlConn = new SqlConnection(connectionString);
        await sqlConn.OpenAsync(cancellationToken);


        var taskIncrement = GetTaskWeight() / statements.Length;
        foreach (var statement in statements)
        {
            progress.Report(("add-constraints", "Adding constraints", taskIncrement, GetTaskWeight()));
            await using var command = new SqlCommand(statement, sqlConn);
            command.CommandTimeout = 3600;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public double GetTaskWeight()
    {
        return 10000;
    }

    private const string Sql = """

                               ALTER TABLE [dbo].[Badges] ADD  CONSTRAINT [PK_Badges__Id] PRIMARY KEY CLUSTERED
                               (
                               	[Id] ASC
                               )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
                               GO

                               ALTER TABLE [dbo].[Comments] ADD  CONSTRAINT [PK_Comments__Id] PRIMARY KEY CLUSTERED
                               (
                               	[Id] ASC
                               )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
                               GO

                               ALTER TABLE [dbo].[LinkTypes] ADD  CONSTRAINT [PK_LinkTypes__Id] PRIMARY KEY CLUSTERED
                               (
                               	[Id] ASC
                               )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
                               GO

                               ALTER TABLE [dbo].[PostHistory] ADD  CONSTRAINT [PK_PostHistory__Id] PRIMARY KEY CLUSTERED
                               (
                               	[Id] ASC
                               )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
                               GO

                               ALTER TABLE [dbo].[PostHistoryTypes] ADD  CONSTRAINT [PK_PostHistoryTypes__Id] PRIMARY KEY CLUSTERED
                               (
                               	[Id] ASC
                               )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
                               GO


                               ALTER TABLE [dbo].[PostLinks] ADD  CONSTRAINT [PK_PostLinks__Id] PRIMARY KEY CLUSTERED
                               (
                               	[Id] ASC
                               )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
                               GO

                               GO
                               ALTER TABLE [dbo].[Posts] ADD
                                   CONSTRAINT [PK_Posts__Id] PRIMARY KEY CLUSTERED
                                   (
                               	    [Id] ASC
                                   )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
                                   
                                   CONSTRAINT [IX_Posts_Id_ParentId] UNIQUE NONCLUSTERED
                                   (
                               	    [Id] ASC,
                               	    [ParentId] ASC
                                   )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
                                   
                                   CONSTRAINT [IX_Posts_Id_OwnerUserId] UNIQUE NONCLUSTERED
                                   (
                               	    [Id] ASC,
                               	    [OwnerUserId] ASC
                                   )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
                                   
                                   CONSTRAINT [IX_Posts_Id_AcceptedAnswerId] UNIQUE NONCLUSTERED
                                   (
                               	    [Id] ASC,
                               	    [AcceptedAnswerId] ASC
                                   )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]

                               GO

                               ALTER TABLE [dbo].[PostTypes] ADD  CONSTRAINT [PK_PostTypes__Id] PRIMARY KEY CLUSTERED
                               (
                               	[Id] ASC
                               )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
                               GO

                               ALTER TABLE [dbo].[Tags] ADD  CONSTRAINT [PK_Tags__Id] PRIMARY KEY CLUSTERED
                               (
                               	[Id] ASC
                               )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
                               GO

                               ALTER TABLE [dbo].[Users] ADD  CONSTRAINT [PK_Users_Id] PRIMARY KEY CLUSTERED
                               (
                               	[Id] ASC
                               )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
                               GO

                               ALTER TABLE [dbo].[Votes] ADD  CONSTRAINT [PK_Votes__Id] PRIMARY KEY CLUSTERED
                               (
                               	[Id] ASC
                               )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
                               GO

                               ALTER TABLE [dbo].[VoteTypes] ADD  CONSTRAINT [PK_VoteType__Id] PRIMARY KEY CLUSTERED
                               (
                               	[Id] ASC
                               )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
                               GO

                               """;
}
