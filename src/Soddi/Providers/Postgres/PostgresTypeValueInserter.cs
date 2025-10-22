using Npgsql;

namespace Soddi.Providers.Postgres;

/// <summary>
/// PostgreSQL type value insertion implementation
/// </summary>
[UsedImplicitly]
public class PostgresTypeValueInserter : ITypeValueInserter
{
    public async Task InsertTypeValuesAsync(IDbConnection connection, IFileSystem fileSystem, string archiveFolder, CancellationToken cancellationToken = default)
    {
        // PostgreSQL doesn't need SET IDENTITY_INSERT
        // SERIAL columns automatically handle identity values
        // We can insert explicit IDs without special commands

        var sql = TypeValuesSql;
        await using var command = new NpgsqlCommand(sql, (NpgsqlConnection)connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private const string TypeValuesSql = @"
-- VoteTypes
INSERT INTO votetypes (id, name) VALUES(1, 'AcceptedByOriginator');
INSERT INTO votetypes (id, name) VALUES(2, 'UpMod');
INSERT INTO votetypes (id, name) VALUES(3, 'DownMod');
INSERT INTO votetypes (id, name) VALUES(4, 'Offensive');
INSERT INTO votetypes (id, name) VALUES(5, 'Favorite');
INSERT INTO votetypes (id, name) VALUES(6, 'Close');
INSERT INTO votetypes (id, name) VALUES(7, 'Reopen');
INSERT INTO votetypes (id, name) VALUES(8, 'BountyStart');
INSERT INTO votetypes (id, name) VALUES(9, 'BountyClose');
INSERT INTO votetypes (id, name) VALUES(10, 'Deletion');
INSERT INTO votetypes (id, name) VALUES(11, 'Undeletion');
INSERT INTO votetypes (id, name) VALUES(12, 'Spam');
INSERT INTO votetypes (id, name) VALUES(13, 'InformModerator');
INSERT INTO votetypes (id, name) VALUES(15, 'ModeratorReview');
INSERT INTO votetypes (id, name) VALUES(16, 'ApproveEditSuggestion');
SELECT setval('votetypes_id_seq', (SELECT MAX(id) FROM votetypes));

-- PostTypes
INSERT INTO posttypes (id, type) VALUES(1, 'Question');
INSERT INTO posttypes (id, type) VALUES(2, 'Answer');
INSERT INTO posttypes (id, type) VALUES(3, 'Wiki');
INSERT INTO posttypes (id, type) VALUES(4, 'TagWikiExerpt');
INSERT INTO posttypes (id, type) VALUES(5, 'TagWiki');
INSERT INTO posttypes (id, type) VALUES(6, 'ModeratorNomination');
INSERT INTO posttypes (id, type) VALUES(7, 'WikiPlaceholder');
INSERT INTO posttypes (id, type) VALUES(8, 'PrivilegeWiki');
SELECT setval('posttypes_id_seq', (SELECT MAX(id) FROM posttypes));

-- LinkTypes
INSERT INTO linktypes (id, type) VALUES(1, 'Linked');
INSERT INTO linktypes (id, type) VALUES(3, 'Duplicate');
SELECT setval('linktypes_id_seq', (SELECT MAX(id) FROM linktypes));

-- PostHistoryTypes
INSERT INTO posthistorytypes (id, type) VALUES(1, 'InitialTitle');
INSERT INTO posthistorytypes (id, type) VALUES(2, 'InitialBody');
INSERT INTO posthistorytypes (id, type) VALUES(3, 'InitialTags');
INSERT INTO posthistorytypes (id, type) VALUES(4, 'EditTitle');
INSERT INTO posthistorytypes (id, type) VALUES(5, 'EditBody');
INSERT INTO posthistorytypes (id, type) VALUES(6, 'EditTags');
INSERT INTO posthistorytypes (id, type) VALUES(7, 'RollbackTitle');
INSERT INTO posthistorytypes (id, type) VALUES(8, 'RollbackBody');
INSERT INTO posthistorytypes (id, type) VALUES(9, 'RollbackTags');
INSERT INTO posthistorytypes (id, type) VALUES(10, 'PostClosed');
INSERT INTO posthistorytypes (id, type) VALUES(11, 'PostReopened');
INSERT INTO posthistorytypes (id, type) VALUES(12, 'PostDeleted');
INSERT INTO posthistorytypes (id, type) VALUES(13, 'PostUndeleted');
INSERT INTO posthistorytypes (id, type) VALUES(14, 'PostLocked');
INSERT INTO posthistorytypes (id, type) VALUES(15, 'PostUnlocked');
INSERT INTO posthistorytypes (id, type) VALUES(16, 'CommunityOwned');
INSERT INTO posthistorytypes (id, type) VALUES(17, 'PostMigrated');
INSERT INTO posthistorytypes (id, type) VALUES(18, 'QuestionMerged');
INSERT INTO posthistorytypes (id, type) VALUES(19, 'QuestionProtected');
INSERT INTO posthistorytypes (id, type) VALUES(20, 'QuestionUnprotected');
INSERT INTO posthistorytypes (id, type) VALUES(21, 'PostDisassociated');
INSERT INTO posthistorytypes (id, type) VALUES(22, 'QuestionUnmerged');
INSERT INTO posthistorytypes (id, type) VALUES(23, 'UnknownDevRelatedEvent');
INSERT INTO posthistorytypes (id, type) VALUES(24, 'SuggestedEditApplied');
INSERT INTO posthistorytypes (id, type) VALUES(25, 'PostTweeted');
INSERT INTO posthistorytypes (id, type) VALUES(26, 'VoteNullificationByDev');
INSERT INTO posthistorytypes (id, type) VALUES(27, 'PostUnmigrated');
INSERT INTO posthistorytypes (id, type) VALUES(28, 'UnknownSuggestionEvent');
INSERT INTO posthistorytypes (id, type) VALUES(29, 'UnknownModeratorEvent');
INSERT INTO posthistorytypes (id, type) VALUES(30, 'UnknownEvent');
INSERT INTO posthistorytypes (id, type) VALUES(31, 'CommentDiscussionMovedToChat');
INSERT INTO posthistorytypes (id, type) VALUES(33, 'PostNoticeAdded');
INSERT INTO posthistorytypes (id, type) VALUES(34, 'PostNoticeRemoved');
INSERT INTO posthistorytypes (id, type) VALUES(35, 'PostMigratedAway');
INSERT INTO posthistorytypes (id, type) VALUES(36, 'PostMigratedHere');
INSERT INTO posthistorytypes (id, type) VALUES(37, 'PostMergeSource');
INSERT INTO posthistorytypes (id, type) VALUES(38, 'PostMergeDestination');
INSERT INTO posthistorytypes (id, type) VALUES(50, 'BumpedByCommunityUser');
INSERT INTO posthistorytypes (id, type) VALUES(52, 'BecameHotNetworkQuestion');
INSERT INTO posthistorytypes (id, type) VALUES(53, 'RemovedFromHotNetworkByMod');
SELECT setval('posthistorytypes_id_seq', (SELECT MAX(id) FROM posthistorytypes));
";
}
