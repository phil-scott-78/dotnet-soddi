using Microsoft.Data.SqlClient;

namespace Soddi.Tasks.SqlServer;

public class AddForeignKeys(string connectionString, bool includePostTags) : ITask
{
    public async Task GoAsync(IProgress<(string taskId, string message, double weight, double maxValue)> progress, CancellationToken cancellationToken)
    {
        await RetryPolicy.Policy.ExecuteAsync(async () => 
        {
            var statements = Sql.Split("GO");
            await using var sqlConn = new SqlConnection(connectionString);
            await sqlConn.OpenAsync(cancellationToken);

            var incrementValue = GetTaskWeight() / statements.Length;

            foreach (var statement in statements)
            {
                await using var command = new SqlCommand(statement, sqlConn);
                await command.ExecuteNonQueryAsync(cancellationToken);
                progress.Report(("createFKs", "Creating foreign keys", incrementValue, GetTaskWeight()));
            }
        });
    }

    public double GetTaskWeight()
    {
        return 50000;
    }
    
    private string Sql
    {
        get
        {
            var s = @"
    ALTER TABLE Posts
    ADD CONSTRAINT FK_Posts_PostTypeId__PostTypes_Id FOREIGN KEY (PostTypeId) REFERENCES PostTypes(Id)
    GO
    ALTER TABLE Posts
    ADD CONSTRAINT FK_Posts_ParentId__Posts_Id FOREIGN KEY (ParentId) REFERENCES Posts(Id)
    GO
    ALTER TABLE Posts
    ADD CONSTRAINT FK_Posts_OwnerUserId__Users_Id FOREIGN KEY (OwnerUserId) REFERENCES Users(Id)
    GO
    ALTER TABLE Posts
    ADD CONSTRAINT FK_Posts_AcceptedAnswerId__Posts_Id FOREIGN KEY (AcceptedAnswerId) REFERENCES Posts(Id)
    GO
    ALTER TABLE Comments
    ADD CONSTRAINT FK_Comments_PostId__Posts_Id FOREIGN KEY (PostId) REFERENCES Posts(Id)
    GO
    ALTER TABLE Comments
    ADD CONSTRAINT FK_Comments_UserId__Users_Id FOREIGN KEY (UserId) REFERENCES Users(Id)
    GO
	ALTER TABLE PostHistory
	ADD CONSTRAINT FK_PostHistory_PostId__Posts_Id FOREIGN KEY (PostId) REFERENCES Posts(Id)
    GO
	ALTER TABLE PostHistory
	ADD CONSTRAINT FK_PostHistory_UserId__Users_Id FOREIGN KEY (UserId) REFERENCES Users(Id)
    GO
	ALTER TABLE PostHistory
	ADD CONSTRAINT FK_PostHistory_PostHistoryTypeId__PostHistoryTypes_Id FOREIGN KEY (PostHistoryTypeId) REFERENCES PostHistoryTypes(Id)
    GO
    ALTER TABLE PostLinks
    ADD CONSTRAINT FK_PostLinks_PostId__Posts_Id FOREIGN KEY (PostId) REFERENCES Posts(Id)
    GO
    ALTER TABLE PostLinks
    ADD CONSTRAINT FK_PostLinks_RelatedPostId__Posts_Id FOREIGN KEY (RelatedPostId) REFERENCES Posts(Id)
    GO
    ALTER TABLE PostLinks
    ADD CONSTRAINT FK_PostLinks_LinkTypeId__LinkTypes_Id FOREIGN KEY (LinkTypeId) REFERENCES LinkTypes(Id)
    GO
    ALTER TABLE Votes
    ADD CONSTRAINT FK_Votes_PostId__Posts_Id FOREIGN KEY (PostId) REFERENCES Posts(Id)
    GO
    ALTER TABLE Votes
    ADD CONSTRAINT FK_Votes_UserId__Users_Id FOREIGN KEY (UserId) REFERENCES Users(Id)
    GO
    ALTER TABLE Votes
    ADD CONSTRAINT FK_Votes_UserId__VoteTypes_Id FOREIGN KEY (VoteTypeId) REFERENCES VoteTypes(Id)
    GO
";

            if (includePostTags)
            {
                s += @"
/****** Object:  Table [dbo].[PostTags]    Script Date: 9/15/2020 7:48:12 PM ******/

    ALTER TABLE PostTags
    ADD CONSTRAINT FK_PostTags_PostId__Posts_Id FOREIGN KEY (PostId) REFERENCES Posts(Id)
    GO
";
            }

            return s;
        }
    }
}
