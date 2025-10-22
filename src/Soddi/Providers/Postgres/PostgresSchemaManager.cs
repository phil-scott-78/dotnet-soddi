using Npgsql;

namespace Soddi.Providers.Postgres;

/// <summary>
/// PostgreSQL schema management implementation
/// </summary>
[UsedImplicitly]
public class PostgresSchemaManager : ISchemaManager
{
    public async Task CreateSchemaAsync(IDbConnection connection, bool includePostTags, CancellationToken cancellationToken = default)
    {
        await CheckIfAlreadyExistsAsync(connection, cancellationToken);

        var sql = GetSchemaCreationSql(includePostTags);
        var statements = sql.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var statement in statements)
        {
            var trimmed = statement.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            await using var command = new NpgsqlCommand(trimmed, (NpgsqlConnection)connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task AddConstraintsAsync(IDbConnection connection, bool skipConstraints, CancellationToken cancellationToken = default)
    {
        if (skipConstraints) return;

        var sql = ConstraintsSql;
        var statements = sql.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var statement in statements)
        {
            var trimmed = statement.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            await using var command = new NpgsqlCommand(trimmed, (NpgsqlConnection)connection);
            command.CommandTimeout = 3600; // constraints can take a while on large datasets
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task AddForeignKeysAsync(IDbConnection connection, CancellationToken cancellationToken = default)
    {
        // Foreign keys are added with NOT VALID to skip validation of existing data
        // This allows imports with orphaned references (common in Stack Overflow data dumps)
        // To find and clean up orphaned data later, run queries like:
        // SELECT * FROM badges WHERE userid NOT IN (SELECT id FROM users);
        // To validate all constraints after cleanup:
        // ALTER TABLE badges VALIDATE CONSTRAINT fk_badges_users;

        var sql = ForeignKeysSql;
        var statements = sql.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var statement in statements)
        {
            var trimmed = statement.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            await using var command = new NpgsqlCommand(trimmed, (NpgsqlConnection)connection);
            command.CommandTimeout = 3600;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task CheckIfAlreadyExistsAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        var sql = @"SELECT table_name
    FROM information_schema.tables
    WHERE table_schema = 'public'
    AND table_name IN ('badges', 'comments', 'linktypes', 'posthistory', 'posthistorytypes', 'postlinks', 'posts', 'posttypes', 'tags', 'users', 'votes', 'votetypes')";

        var tablesThatAlreadyExist = new List<string>();

        await using (var command = new NpgsqlCommand(sql, (NpgsqlConnection)connection))
        await using (var dr = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await dr.ReadAsync(cancellationToken))
            {
                tablesThatAlreadyExist.Add(dr.GetString(0));
            }
        }

        if (tablesThatAlreadyExist.Count > 0)
        {
            throw new SoddiException(
                $"Schema already exists in database.\n\tTables: {string.Join(", ", tablesThatAlreadyExist)}.\n\nTo drop and recreate the database use the --dropAndCreate option");
        }
    }

    private string GetSchemaCreationSql(bool includePostTags)
    {
        // PostgreSQL translations:
        // - [int] IDENTITY(1,1) → SERIAL or INTEGER GENERATED ALWAYS AS IDENTITY
        // - [nvarchar](n) → VARCHAR(n)
        // - [nvarchar](max) → TEXT
        // - [varchar](n) → VARCHAR(n)
        // - [ntext] → TEXT
        // - [datetime] → TIMESTAMP
        // - [bit] → BOOLEAN
        // - [char](n) → CHAR(n)
        // - Table names lowercase for PostgreSQL convention
        // - Remove ON [PRIMARY] and TEXTIMAGE_ON [PRIMARY]

        var s = @"
CREATE TABLE badges(
	id SERIAL PRIMARY KEY,
	name VARCHAR(40) NOT NULL,
	userid INTEGER NOT NULL,
	date TIMESTAMP NOT NULL,
    class INTEGER NOT NULL,
    tagbased BOOLEAN NOT NULL
);

CREATE TABLE comments(
	id SERIAL PRIMARY KEY,
	creationdate TIMESTAMP NOT NULL,
	postid INTEGER NOT NULL,
	score INTEGER NULL,
	text VARCHAR(700) NOT NULL,
	userid INTEGER NULL,
    contentlicense VARCHAR(250) NULL DEFAULT 'CC BY-SA 4.0'
);

CREATE TABLE linktypes(
	id SERIAL PRIMARY KEY,
	type VARCHAR(50) NOT NULL
);

CREATE TABLE posthistory(
	id SERIAL PRIMARY KEY,
	posthistorytypeid INTEGER NOT NULL,
	postid INTEGER NOT NULL,
	revisionguid CHAR(36) NOT NULL,
	creationdate TIMESTAMP NOT NULL,
	userid INTEGER NULL,
	userdisplayname VARCHAR(40) NULL,
	comment TEXT NULL,
	text TEXT NULL,
    contentlicense VARCHAR(250) NULL DEFAULT 'CC BY-SA 4.0'
);

CREATE TABLE posthistorytypes(
	id SERIAL PRIMARY KEY,
	type VARCHAR(50) NOT NULL
);

CREATE TABLE postlinks(
	id SERIAL PRIMARY KEY,
	creationdate TIMESTAMP NOT NULL,
	postid INTEGER NOT NULL,
	relatedpostid INTEGER NOT NULL,
	linktypeid INTEGER NOT NULL
);

CREATE TABLE posts(
	id SERIAL PRIMARY KEY,
	acceptedanswerid INTEGER NULL,
	answercount INTEGER NULL,
	body TEXT NOT NULL,
	closeddate TIMESTAMP NULL,
	commentcount INTEGER NULL,
	communityowneddate TIMESTAMP NULL,
    contentlicense VARCHAR(250) NULL DEFAULT 'CC BY-SA 4.0',
	creationdate TIMESTAMP NOT NULL,
	favoritecount INTEGER NULL,
	lastactivitydate TIMESTAMP NOT NULL,
	lasteditdate TIMESTAMP NULL,
	lasteditordisplayname VARCHAR(40) NULL,
	lasteditoruserid INTEGER NULL,
	owneruserid INTEGER NULL,
	parentid INTEGER NULL,
	posttypeid INTEGER NOT NULL,
	score INTEGER NOT NULL,
	tags VARCHAR(150) NULL,
	title VARCHAR(250) NULL,
	viewcount INTEGER NULL
);

CREATE TABLE posttypes(
	id SERIAL PRIMARY KEY,
	type VARCHAR(50) NOT NULL
);

CREATE TABLE tags(
	id SERIAL PRIMARY KEY,
	tagname VARCHAR(150) NOT NULL,
	count INTEGER NOT NULL,
	excerptpostid INTEGER NULL,
	wikipostid INTEGER NULL
);

CREATE TABLE users(
	id SERIAL PRIMARY KEY,
	aboutme TEXT NULL,
	age INTEGER NULL,
	creationdate TIMESTAMP NOT NULL,
	displayname VARCHAR(40) NOT NULL,
	downvotes INTEGER NOT NULL,
	emailhash VARCHAR(40) NULL,
	lastaccessdate TIMESTAMP NOT NULL,
	location VARCHAR(100) NULL,
	reputation INTEGER NOT NULL,
	upvotes INTEGER NOT NULL,
	views INTEGER NOT NULL,
	websiteurl VARCHAR(200) NULL,
	accountid INTEGER NULL
);

CREATE TABLE votes(
	id SERIAL PRIMARY KEY,
	postid INTEGER NOT NULL,
	userid INTEGER NULL,
	bountyamount INTEGER NULL,
	votetypeid INTEGER NOT NULL,
	creationdate TIMESTAMP NOT NULL
);

CREATE TABLE votetypes(
	id SERIAL PRIMARY KEY,
	name VARCHAR(50) NOT NULL
);
";

        if (includePostTags)
        {
            s += @"
CREATE TABLE posttags (
  postid INTEGER NOT NULL,
  tag VARCHAR(50) NOT NULL,
  PRIMARY KEY (postid, tag)
);
";
        }

        // PostgreSQL uses COMMENT ON for column descriptions instead of extended properties
        s += @"
COMMENT ON COLUMN posts.acceptedanswerid IS 'Id of the accepted answer. Only present if PostTypeId = 1 (question).';
COMMENT ON COLUMN posts.body IS 'The body as rendered HTML.';
COMMENT ON COLUMN posts.communityowneddate IS 'The date the post became community owned. Present only if post is community wiki''d.';
COMMENT ON COLUMN posts.lastactivitydate IS 'The date and time of the post''s most recent activity.';
COMMENT ON COLUMN posts.lasteditdate IS 'The date and time of the most recent edit to the post.';
COMMENT ON COLUMN posts.owneruserid IS 'User Id of the owner. Always -1 for tag wiki entries, i.e. the community user owns them.';
COMMENT ON COLUMN posts.parentid IS 'The Id of the Parent. Only present if PostTypeId = 2 (answer).';

COMMENT ON COLUMN users.aboutme IS 'The user''s profile as rendered HTML.';
COMMENT ON COLUMN users.downvotes IS 'The number of downvotes a user has cast.';
COMMENT ON COLUMN users.emailhash IS 'Always blank.';
COMMENT ON COLUMN users.upvotes IS 'The number of upvotes a user has cast.';
COMMENT ON COLUMN users.views IS 'The number of times the profile has been viewed.';
COMMENT ON COLUMN users.accountid IS 'The user''s stack exchange network profile id.';

COMMENT ON COLUMN badges.class IS 'The class of the badge. 1 = Gold, 2 = Silver, 3 = Bronze.';
COMMENT ON COLUMN badges.tagbased IS 'True if badge is for a tag, otherwise it is a named badge.';

COMMENT ON COLUMN posthistory.revisionguid IS 'At times more than one type of history record can be recorded by a single action. All of these will be grouped using the same RevisionGUID.';
COMMENT ON COLUMN posthistory.text IS 'A raw version of the new value for a given revision.';

COMMENT ON COLUMN votes.bountyamount IS 'The bounty amount. Present only if VoteTypeId in (8, 9)';
COMMENT ON COLUMN votes.userid IS 'The user Id of the voter. Present only if VoteTypeId in (5,8).';
";

        return s;
    }

    private const string ConstraintsSql = @"
CREATE INDEX ix_badges_userid ON badges(userid);

CREATE INDEX ix_comments_postid ON comments(postid);
CREATE INDEX ix_comments_userid ON comments(userid);

CREATE INDEX ix_posthistory_postid ON posthistory(postid);
CREATE INDEX ix_posthistory_userid ON posthistory(userid);

CREATE INDEX ix_postlinks_postid ON postlinks(postid);
CREATE INDEX ix_postlinks_relatedpostid ON postlinks(relatedpostid);

CREATE INDEX ix_posts_acceptedanswerid ON posts(acceptedanswerid);
CREATE INDEX ix_posts_owneruserid ON posts(owneruserid);
CREATE INDEX ix_posts_parentid ON posts(parentid);

CREATE UNIQUE INDEX ak_tags_tagname ON tags(tagname);

CREATE INDEX ix_votes_postid ON votes(postid);
";

    private const string ForeignKeysSql = @"
-- Using NOT VALID to add foreign keys without validating existing data
-- This allows imports to succeed even with orphaned references in historical data
-- while still enforcing constraints on future inserts/updates
ALTER TABLE badges ADD CONSTRAINT fk_badges_users FOREIGN KEY (userid) REFERENCES users(id) NOT VALID;

ALTER TABLE comments ADD CONSTRAINT fk_comments_posts FOREIGN KEY (postid) REFERENCES posts(id) NOT VALID;
ALTER TABLE comments ADD CONSTRAINT fk_comments_users FOREIGN KEY (userid) REFERENCES users(id) NOT VALID;

ALTER TABLE posthistory ADD CONSTRAINT fk_posthistory_posthistorytypes FOREIGN KEY (posthistorytypeid) REFERENCES posthistorytypes(id) NOT VALID;
ALTER TABLE posthistory ADD CONSTRAINT fk_posthistory_posts FOREIGN KEY (postid) REFERENCES posts(id) NOT VALID;
ALTER TABLE posthistory ADD CONSTRAINT fk_posthistory_users FOREIGN KEY (userid) REFERENCES users(id) NOT VALID;

ALTER TABLE postlinks ADD CONSTRAINT fk_postlinks_linktypes FOREIGN KEY (linktypeid) REFERENCES linktypes(id) NOT VALID;
ALTER TABLE postlinks ADD CONSTRAINT fk_postlinks_posts FOREIGN KEY (postid) REFERENCES posts(id) NOT VALID;
ALTER TABLE postlinks ADD CONSTRAINT fk_postlinks_posts_related FOREIGN KEY (relatedpostid) REFERENCES posts(id) NOT VALID;

ALTER TABLE posts ADD CONSTRAINT fk_posts_posttypes FOREIGN KEY (posttypeid) REFERENCES posttypes(id) NOT VALID;
ALTER TABLE posts ADD CONSTRAINT fk_posts_users FOREIGN KEY (owneruserid) REFERENCES users(id) NOT VALID;

ALTER TABLE votes ADD CONSTRAINT fk_votes_posts FOREIGN KEY (postid) REFERENCES posts(id) NOT VALID;
ALTER TABLE votes ADD CONSTRAINT fk_votes_votetypes FOREIGN KEY (votetypeid) REFERENCES votetypes(id) NOT VALID;
";
}
