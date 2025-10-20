using Microsoft.Data.SqlClient;

namespace Soddi.Tasks.SqlServer;

public class CreateSchema(string connectionString, bool includePostTags) : ITask
{
    public async Task GoAsync(IProgress<(string taskId, string message, double weight, double maxValue)> progress, CancellationToken cancellationToken)
    {
        await CheckIfAlreadyExists(cancellationToken);

        var statements = Sql.Split("GO");
        await using var sqlConn = new SqlConnection(connectionString);
        await sqlConn.OpenAsync(cancellationToken);

        var incrementValue = GetTaskWeight() / statements.Length;

        foreach (var statement in statements)
        {
            await using var command = new SqlCommand(statement, sqlConn);
            await command.ExecuteNonQueryAsync(cancellationToken);
            progress.Report(("createSchema", "Creating objects", incrementValue, GetTaskWeight()));
        }
    }

    private async Task CheckIfAlreadyExists(CancellationToken cancellationToken)
    {
        var sql = @"SELECT TABLE_NAME
    FROM INFORMATION_SCHEMA.TABLES 
    WHERE TABLE_SCHEMA = 'dbo' 
    AND  TABLE_NAME in ('Badges', 'Comments', 'LinkTypes', 'PostHistory', 'PostHistoryTypes', 'PostLinks', 'Posts', 'PostTypes', 'Tags', 'Users', 'Votes', 'VoteTypes')";

        await using var sqlConn = new SqlConnection(connectionString);
        await sqlConn.OpenAsync(cancellationToken);
        await using var sqlCommand = new SqlCommand(sql, sqlConn);

        var tablesThatAlreadyExist = new List<string>();
        var dr = await sqlCommand.ExecuteReaderAsync(cancellationToken);
        while (await dr.ReadAsync(cancellationToken))
        {
            tablesThatAlreadyExist.Add(dr.GetString(0));
        }

        if (tablesThatAlreadyExist.Count > 0)
        {
            throw new SoddiException(
                $"Schema already exists in database {connectionString}.\n\tTables: {string.Join(", ", tablesThatAlreadyExist)}.\n\nTo drop and recreate the database use the --dropAndCreate option");
        }
    }

    public double GetTaskWeight()
    {
        return 10000;
    }

    private string Sql
    {
        get
        {
            var s = @"
CREATE TABLE [dbo].[Badges](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](40) NOT NULL,
	[UserId] [int] NOT NULL,
	[Date] [datetime] NOT NULL,
    [Class] [int] NOT NULL,
    [TagBased] [bit] NOT NULL,
) ON [PRIMARY]
GO

/****** Object:  Table [dbo].[Comments]    Script Date: 9/15/2020 7:48:12 PM ******/
CREATE TABLE [dbo].[Comments](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[CreationDate] [datetime] NOT NULL,
	[PostId] [int] NOT NULL,
	[Score] [int] NULL,
	[Text] [nvarchar](700) NOT NULL,
	[UserId] [int] NULL,
    [ContentLicense] [nvarchar](250) NULL DEFAULT 'CC BY-SA 4.0',
) ON [PRIMARY]
GO

/****** Object:  Table [dbo].[LinkTypes]    Script Date: 9/15/2020 7:48:12 PM ******/
CREATE TABLE [dbo].[LinkTypes](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Type] [varchar](50) NOT NULL,
) ON [PRIMARY]
GO

/****** Object:  Table [dbo].[PostHistory]    Script Date: 9/15/2020 7:48:12 PM ******/
CREATE TABLE [dbo].[PostHistory](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[PostHistoryTypeId] [int] NOT NULL,
	[PostId] [int] NOT NULL,
	[RevisionGUID] [char](36) NOT NULL,
	[CreationDate] [datetime] NOT NULL,
	[UserId] [int] NULL,
	[UserDisplayName] [nvarchar](40) NULL,
	[Comment] [ntext] NULL,
	[Text] [ntext] NULL,
    [ContentLicense] [nvarchar](250) NULL DEFAULT 'CC BY-SA 4.0',
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

/****** Object:  Table [dbo].[PostHistoryTypes]    Script Date: 9/15/2020 7:48:12 PM ******/
CREATE TABLE [dbo].[PostHistoryTypes](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Type] [nvarchar](50) NOT NULL,
) ON [PRIMARY]
GO

/****** Object:  Table [dbo].[PostLinks]    Script Date: 9/15/2020 7:48:12 PM ******/
CREATE TABLE [dbo].[PostLinks](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[CreationDate] [datetime] NOT NULL,
	[PostId] [int] NOT NULL,
	[RelatedPostId] [int] NOT NULL,
	[LinkTypeId] [int] NOT NULL,
) ON [PRIMARY]
GO

/****** Object:  Table [dbo].[Posts]    Script Date: 9/15/2020 7:48:12 PM ******/
CREATE TABLE [dbo].[Posts](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[AcceptedAnswerId] [int] NULL,
	[AnswerCount] [int] NULL,
	[Body] [nvarchar](max) NOT NULL,
	[ClosedDate] [datetime] NULL,
	[CommentCount] [int] NULL,
	[CommunityOwnedDate] [datetime] NULL,
    [ContentLicense] [nvarchar](250) NULL DEFAULT 'CC BY-SA 4.0',
	[CreationDate] [datetime] NOT NULL,
	[FavoriteCount] [int] NULL,
	[LastActivityDate] [datetime] NOT NULL,
	[LastEditDate] [datetime] NULL,
	[LastEditorDisplayName] [nvarchar](40) NULL,
	[LastEditorUserId] [int] NULL,
	[OwnerUserId] [int] NULL,
	[ParentId] [int] NULL,
	[PostTypeId] [int] NOT NULL,
	[Score] [int] NOT NULL,
	[Tags] [nvarchar](150) NULL,
	[Title] [nvarchar](250) NULL,
	[ViewCount] [int] NULL,
) ON [PRIMARY] 
GO

/****** Object:  Table [dbo].[PostTypes]    Script Date: 9/15/2020 7:48:12 PM ******/
CREATE TABLE [dbo].[PostTypes](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Type] [nvarchar](50) NOT NULL,
) ON [PRIMARY]
GO

/****** Object:  Table [dbo].[Tags]    Script Date: 9/15/2020 7:48:12 PM ******/
CREATE TABLE [dbo].[Tags](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[TagName] [nvarchar](150) NOT NULL,
	[Count] [int] NOT NULL,
	[ExcerptPostId] [int] NULL,
	[WikiPostId] [int] NULL,
) ON [PRIMARY]
GO

/****** Object:  Table [dbo].[Users]    Script Date: 9/15/2020 7:48:12 PM ******/
CREATE TABLE [dbo].[Users](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[AboutMe] [nvarchar](max) NULL,
	[Age] [int] NULL,
	[CreationDate] [datetime] NOT NULL,
	[DisplayName] [nvarchar](40) NOT NULL,
	[DownVotes] [int] NOT NULL,
	[EmailHash] [nvarchar](40) NULL,
	[LastAccessDate] [datetime] NOT NULL,
	[Location] [nvarchar](100) NULL,
	[Reputation] [int] NOT NULL,
	[UpVotes] [int] NOT NULL,
	[Views] [int] NOT NULL,
	[WebsiteUrl] [nvarchar](200) NULL,
	[AccountId] [int] NULL,
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

/****** Object:  Table [dbo].[Votes]    Script Date: 9/15/2020 7:48:12 PM ******/
CREATE TABLE [dbo].[Votes](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[PostId] [int] NOT NULL,
	[UserId] [int] NULL,
	[BountyAmount] [int] NULL,
	[VoteTypeId] [int] NOT NULL,
	[CreationDate] [datetime] NOT NULL,
) ON [PRIMARY]
GO

/****** Object:  Table [dbo].[VoteTypes]    Script Date: 9/15/2020 7:48:12 PM ******/
CREATE TABLE [dbo].[VoteTypes](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [varchar](50) NOT NULL,
) ON [PRIMARY]
GO
";

            if (includePostTags)
            {
                s += @"
/****** Object:  Table [dbo].[PostTags]    Script Date: 9/15/2020 7:48:12 PM ******/

CREATE TABLE [dbo].[PostTags] (
  [PostId] [INT] /* IDENTITY */    NOT NULL,
  [Tag]    [NVARCHAR](50)    NOT NULL
  , CONSTRAINT [PK_PostTags__PostId_Tag] PRIMARY KEY CLUSTERED ( [PostId] ASC,[Tag] ASC ) ON [PRIMARY]
  ) ON [PRIMARY]
";
            }

            s += @"
/****** Post column comments  ******/

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Id of the accepted answer. Only present if PostTypeId = 1 (question).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Posts', @level2type=N'COLUMN',@level2name=N'AcceptedAnswerId'
GO

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The body as rendered HTML.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Posts', @level2type=N'COLUMN',@level2name=N'Body'
GO

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The date the post became community owned. Present only if post is community wiki''d.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Posts', @level2type=N'COLUMN',@level2name=N'CommunityOwnedDate'
GO

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The date and time of the post''s most recent activity.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Posts', @level2type=N'COLUMN',@level2name=N'LastActivityDate'
GO

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The date and time of the most recent edit to the post.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Posts', @level2type=N'COLUMN',@level2name=N'LastEditDate'
GO

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'User Id of the owner. Always -1 for tag wiki entries, i.e. the community user owns them.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Posts', @level2type=N'COLUMN',@level2name=N'OwnerUserId'
GO

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The Id of the Parent. Only present if PostTypeId = 2 (answer).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Posts', @level2type=N'COLUMN',@level2name=N'ParentId'
GO


/****** User column comments  ******/

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The user''s profile as rendered HTML.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Users', @level2type=N'COLUMN',@level2name=N'AboutMe'
GO

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The number of downvotes a user has cast.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Users', @level2type=N'COLUMN',@level2name=N'DownVotes'
GO

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Always blank.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Users', @level2type=N'COLUMN',@level2name=N'EmailHash'
GO

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The number of upvotes a user has cast.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Users', @level2type=N'COLUMN',@level2name=N'UpVotes'
GO

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The number of times the profile has been viewed.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Users', @level2type=N'COLUMN',@level2name=N'Views'
GO

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The user''s stack exchange network profile id.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Users', @level2type=N'COLUMN',@level2name=N'AccountId'
GO


/****** User column comments  ******/

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The class of the badge. 1 = Gold, 2 = Silver, 3 = Bronze.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Badges', @level2type=N'COLUMN',@level2name=N'Class'
GO

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'True if badge is for a tag, otherwise it is a named badge.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Badges', @level2type=N'COLUMN',@level2name=N'TagBased'
GO


/****** Post History column comments  ******/

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'At times more than one type of history record can be recorded by a single action. All of these will be grouped using the same RevisionGUID.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PostHistory', @level2type=N'COLUMN',@level2name=N'RevisionGUID'
GO

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'A raw version of the new value for a given revision. 

If PostHistoryTypeId in (10,11,12,13,14,15,19,20,35) this column will contain a JSON encoded string with all users who have voted for the PostHistoryTypeId.

If it is a duplicate close vote, the JSON string will contain an array of original questions as OriginalQuestionIds.

If PostHistoryTypeId = 17 this column will contain migration details of either from <url> or to <url>.' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'PostHistory', @level2type=N'COLUMN',@level2name=N'Text'
GO


/****** Vote column comments  ******/

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The bounty amount. Present only if VoteTypeId in (8, 9)' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Votes', @level2type=N'COLUMN',@level2name=N'Id'
GO

EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'The user Id of the voter. Present only if VoteTypeId in (5,8).' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'Votes', @level2type=N'COLUMN',@level2name=N'UserId'
GO





";

            return s;
        }
    }
}
