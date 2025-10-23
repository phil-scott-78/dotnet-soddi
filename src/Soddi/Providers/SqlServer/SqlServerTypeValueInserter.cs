using Microsoft.Data.SqlClient;

namespace Soddi.Providers.SqlServer;

/// <summary>
/// SQL Server type value insertion implementation
/// </summary>
[UsedImplicitly]
public class SqlServerTypeValueInserter : ITypeValueInserter
{
    public async Task InsertTypeValuesAsync(IDbConnection connection, IFileSystem fileSystem, string archiveFolder, CancellationToken cancellationToken = default)
    {
        await SqlServerRetryPolicy.Policy.ExecuteAsync(async () =>
        {
            await using var command = new SqlCommand(TypeValuesSql, (SqlConnection)connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        });
    }

    private const string TypeValuesSql = @"
SET IDENTITY_INSERT [VoteTypes] ON
INSERT [VoteTypes] ([Id], [Name]) VALUES(1, N'AcceptedByOriginator')
INSERT [VoteTypes] ([Id], [Name]) VALUES(2, N'UpMod')
INSERT [VoteTypes] ([Id], [Name]) VALUES(3, N'DownMod')
INSERT [VoteTypes] ([Id], [Name]) VALUES(4, N'Offensive')
INSERT [VoteTypes] ([Id], [Name]) VALUES(5, N'Favorite')
INSERT [VoteTypes] ([Id], [Name]) VALUES(6, N'Close')
INSERT [VoteTypes] ([Id], [Name]) VALUES(7, N'Reopen')
INSERT [VoteTypes] ([Id], [Name]) VALUES(8, N'BountyStart')
INSERT [VoteTypes] ([Id], [Name]) VALUES(9, N'BountyClose')
INSERT [VoteTypes] ([Id], [Name]) VALUES(10,N'Deletion')
INSERT [VoteTypes] ([Id], [Name]) VALUES(11,N'Undeletion')
INSERT [VoteTypes] ([Id], [Name]) VALUES(12,N'Spam')
INSERT [VoteTypes] ([Id], [Name]) VALUES(13,N'InformModerator')
INSERT [VoteTypes] ([Id], [Name]) VALUES(15,N'ModeratorReview')
INSERT [VoteTypes] ([Id], [Name]) VALUES(16,N'ApproveEditSuggestion')
SET IDENTITY_INSERT [VoteTypes] OFF
DBCC CHECKIDENT('VoteTypes', RESEED)

SET IDENTITY_INSERT [PostTypes] ON
INSERT [PostTypes] ([Id], [Type]) VALUES(1, N'Question')
INSERT [PostTypes] ([Id], [Type]) VALUES(2, N'Answer')
INSERT [PostTypes] ([Id], [Type]) VALUES(3, N'Wiki')
INSERT [PostTypes] ([Id], [Type]) VALUES(4, N'TagWikiExerpt')
INSERT [PostTypes] ([Id], [Type]) VALUES(5, N'TagWiki')
INSERT [PostTypes] ([Id], [Type]) VALUES(6, N'ModeratorNomination')
INSERT [PostTypes] ([Id], [Type]) VALUES(7, N'WikiPlaceholder')
INSERT [PostTypes] ([Id], [Type]) VALUES(8, N'PrivilegeWiki')
SET IDENTITY_INSERT [PostTypes] OFF
DBCC CHECKIDENT('PostTypes', RESEED)

SET IDENTITY_INSERT [LinkTypes] ON
INSERT [LinkTypes] ([Id], [Type]) VALUES(1, N'Linked')
INSERT [LinkTypes] ([Id], [Type]) VALUES(3, N'Duplicate')
SET IDENTITY_INSERT [LinkTypes] OFF
DBCC CHECKIDENT('LinkTypes', RESEED)

SET IDENTITY_INSERT [PostHistoryTypes] ON
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(1, N'InitialTitle')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(2, N'InitialBody')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(3, N'InitialTags')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(4, N'EditTitle')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(5, N'EditBody')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(6, N'EditTags')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(7, N'RollbackTitle')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(8, N'RollbackBody')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(9, N'RollbackTags')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(10, N'PostClosed')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(11, N'PostReopened')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(12, N'PostDeleted')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(13, N'PostUndeleted')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(14, N'PostLocked')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(15, N'PostUnlocked')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(16, N'CommunityOwned')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(17, N'PostMigrated')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(18, N'QuestionMerged')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(19, N'QuestionProtected')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(20, N'QuestionUnprotected')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(21, N'PostDisassociated')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(22, N'QuestionUnmerged')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(23, N'UnknownDevRelatedEvent')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(24, N'SuggestedEditApplied')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(25, N'PostTweeted')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(26, N'VoteNullificationByDev')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(27, N'PostUnmigrated')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(28, N'UnknownSuggestionEvent')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(29, N'UnknownModeratorEvent')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(30, N'UnknownEvent')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(31, N'CommentDiscussionMovedToChat')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(33, N'PostNoticeAdded')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(34, N'PostNoticeRemoved')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(35, N'PostMigratedAway')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(36, N'PostMigratedHere')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(37, N'PostMergeSource')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(38, N'PostMergeDestination')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(50, N'BumpedByCommunityUser')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(52, N'BecameHotNetworkQuestion')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(53, N'RemovedFromHotNetworkByMod')
INSERT [PostHistoryTypes] ([Id], [Type]) VALUES(66, N'Created from Wizard')
SET IDENTITY_INSERT [PostHistoryTypes] OFF
DBCC CHECKIDENT('PostHistoryTypes', RESEED)
";
}
