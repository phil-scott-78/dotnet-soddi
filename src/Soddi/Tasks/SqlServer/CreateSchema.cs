using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Soddi.Tasks.SqlServer
{
    public class CreateSchema : ITask
    {
        private readonly string _connectionString;
        private readonly bool _includePostTags;

        public CreateSchema(string connectionString, bool includePostTags)
        {
            _connectionString = connectionString;
            _includePostTags = includePostTags;
        }

        public void Go(IProgress<(string taskId, string message, double weight, double maxValue)> progress)
        {
            CheckIfAlreadyExists();

            var statements = Sql.Split("GO");
            using var sqlConn = new SqlConnection(_connectionString);
            sqlConn.Open();

            var incrementValue = GetTaskWeight() / statements.Length;

            foreach (var statement in statements)
            {
                using var command = new SqlCommand(statement, sqlConn);
                command.ExecuteNonQuery();
                progress.Report(("createSchema", "Creating objects", incrementValue, GetTaskWeight()));
            }
        }

        private void CheckIfAlreadyExists()
        {
            var sql = @"SELECT TABLE_NAME
    FROM INFORMATION_SCHEMA.TABLES 
    WHERE TABLE_SCHEMA = 'dbo' 
    AND  TABLE_NAME in ('Badges', 'Comments', 'LinkTypes', 'PostHistory', 'PostHistoryTypes', 'PostLinks', 'Posts', 'PostTypes', 'Tags', 'Users', 'Votes', 'VoteTypes')";

            using var sqlConn = new SqlConnection(_connectionString);
            sqlConn.Open();
            using var sqlCommand = new SqlCommand(sql, sqlConn);

            var tablesThatAlreadyExist = new List<string>();
            using var dr = sqlCommand.ExecuteReader();
            while (dr.Read())
            {
                tablesThatAlreadyExist.Add(dr.GetString(0));
            }

            if (tablesThatAlreadyExist.Count > 0)
            {
                throw new SoddiException(
                    $"Schema already exists in database {_connectionString}.\n\tTables: {string.Join(", ", tablesThatAlreadyExist)}.\n\nTo drop and recreate the database use the --dropAndCreate option");
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
    [ContentLicense] [nvarchar](250) NOT NULL,
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
    [ContentLicense] [nvarchar](250) NOT NULL,
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

                if (_includePostTags)
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

                return s;
            }
        }
    }
}
