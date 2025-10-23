# DotNet-Soddi (Stack Overflow Data Dump Importer)

## Install

![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/dotnet-soddi)

```bash
dotnet tool install --global soddi --version 0.5
```

Inspirited by the original [Soddi](https://github.com/BrentOzarULTD/soddi), DotNet-Soddi is a console application that
assists in not just importing the Stack Overflow data dumps, but also obtaining them.

## Features

- Discover archives from the command line.
- Importing data from xml files or stream directly from the .7z archive files.
- Download archive files from the command line via HTTP based on their site name.
- Download archive files via BitTorrent based on their site name.

## Supported Databases

- **SQL Server** (default)
- **PostgreSQL**

Use the `--provider` flag with the import command to specify your database provider (see examples below).

## Limitations

Not all features of the original Soddi are supported.

- Full Text isn't supported.

## Download and install a database in two lines

### SQL Server (default)

The following command will download the archive for the math.stackexchange.com, then import it into an existing database
named math.stackexchange.com

```bash
soddi download math
soddi import math -d math.stackexchange.com
```

### PostgreSQL

```bash
soddi download math
soddi import math --provider postgres --connectionString "Host=localhost;Username=postgres;Password=yourpassword" -d math_stackexchange
```

Because of the size of the database and the bandwidth of archive.org, you might be better off using the torrent option.
The following command will do everything required to connect to the appropriate trackers and peers to download the math
database and exit upon completion.

```bash
soddi torrent math
```

## Usage

### `soddi download`

Downloads a Stack Overflow site archive via HTTP from archive.org.

```bash
USAGE:

download archive for aviation.stackexchange.com:
  soddi download aviation
download archive for math.stackexchange.com to a particular folder:
  soddi download --output c:\stack-data math
pick from archives containing stack and download:
  soddi download --pick stack

  -o, --output        Output folder
  -p, --pick          (Default: false) Pick from a list of archives to download
  --help              Display this help screen.
  --version           Display version information.
  Archive (pos. 0)    Required. Archive to download
```

### `soddi list`

List available Stack Overflow data dumps.

```bash
USAGE:

List all archives:
  soddi list
List all archives containing the letters "av":
  soddi list av
List all archives containing the letters "av" including meta sites:
  soddi list --includeMeta av

  --includeMeta       (Default: false) Include meta databases.
  --help              Display this help screen.
  --version           Display version information.
  Pattern (pos. 0)    (Default: ) Pattern to include (e.g. "av" includes all archives containing "av").
```

### `soddi import`

Import data from a 7z archive or folder

```bash
USAGE:

Import data using defaults (SQL Server):
  soddi import math.stackexchange.com.7z
Import data from a folder containing a collection of .7z or .xml files:
  soddi import stackoverflow
Import data using a connection string and database name:
  soddi import --connectionString "Server=(local)\Sql2017;User Id=admin;password=t3ddy" --database math
  math.stackexchange.com.7z
Import data using defaults and create database:
  soddi import --dropAndCreate math.stackexchange.com.7z
Import data using defaults without constraints:
  soddi import --skipConstraints math.stackexchange.com.7z

PostgreSQL examples:
  soddi import math.stackexchange.com.7z --provider postgres --connectionString "Host=localhost;Username=postgres;Password=yourpassword"
  soddi import stackoverflow --provider postgresql --connectionString "Host=myserver;Port=5432;Database=postgres;Username=admin;Password=secret" -d stackoverflow
  soddi import math.stackexchange.com.7z --provider pg --dropAndCreate --connectionString "Host=localhost;Username=postgres;Password=yourpassword"

  -d, --database            Name of database. If omitted the name of the file or folder will be used.
  -c, --connectionString    Connection string to server.
                            SQL Server (default): Server=.;Integrated Security=true
                            PostgreSQL: Host=localhost;Username=postgres;Password=yourpassword
                            Initial catalog/database in the connection string will be ignored and the --database
                            parameter will be used for the database name.
  --provider                (Default: sqlserver) Database provider to use. Options: sqlserver, postgres (aliases:
                            postgresql, pg). Case-insensitive.
  --dropAndCreate           (Default: false) Drop and recreate database. If a database already exists with this name it
                            will be dropped. Then a database will be created in the default server file location with
                            the default server options.
  --skipConstraints         (Default: false) Skip adding primary keys and unique constraints.
  --skipTags                (Default: false) Skip adding PostTags table.
  --help                    Display this help screen.
  --version                 Display version information.
  Path (pos. 0)             Required. File or folder containing xml archives. The file must be a .7z file. If using a
                            folder it can contain either .7z or .xml content
```

### `soddi torrent`

Download database via BitTorrent. For larger databases this might prove significantly faster.

```bash
USAGE:

Download files associated with the math site from the torrent file:
  soddi torrent math
Download to a specific folder:
  soddi torrent --output "c:\torrent files" math
Enable port forwarding:
  soddi torrent --portForward math
Pick from archives containing "stack":
  soddi torrent --pick stack

  -o, --output         Output folder
  -f, --portForward    (Default: false) Experimental. Enable port forwarding
  -p, --pick           (Default: false) Pick from a list of archives to download
  --help               Display this help screen.
  --version            Display version information.
  Archive (pos. 0)     Required. Archive to download
  ```
  
### `soddi brent`

Super experimental. Downloads and extracts one of the [BrentO provided databases](https://www.brentozar.com/archive/2015/10/how-to-download-the-stack-overflow-database-via-bittorrent/) via BitTorrent.

If you don't supply an `ARCHIVE_NAME` you will be prompted for one.

```bash
USAGE:
    soddi brent [ARCHIVE_NAME] [OPTIONS]

EXAMPLES:
    soddi brent
    soddi brent small
    soddi brent medium
    soddi brent large
    soddi brent extra-large

ARGUMENTS:
    [ARCHIVE_NAME]    Archive to download

OPTIONS:
    -h, --help              Prints help information
    -o, --output            Output folder
        --skipExtraction    Don't extract the downloaded 7z files
    -f, --portForward       Experimental. Enable port forwarding

```

## PostgreSQL-Specific Notes

When using PostgreSQL as the database provider:

### Schema Conventions
- Table names are created in lowercase (PostgreSQL convention)
- Uses PostgreSQL native types: `SERIAL`, `TEXT`, `TIMESTAMP`, `BOOLEAN`, `VARCHAR(n)`

### Performance
- Bulk insert uses PostgreSQL's high-performance `COPY` command (similar to SQL Server's `SqlBulkCopy`)
- Significantly faster than individual INSERT statements

### Foreign Key Constraints
- Foreign keys are added with the `NOT VALID` clause to allow orphaned references
- This is common in Stack Overflow data dumps where referenced records may not exist
- To find orphaned references after import:
  ```sql
  -- Example: Find badges with non-existent users
  SELECT * FROM badges WHERE userid NOT IN (SELECT id FROM users);
  ```
- To validate constraints after import:
  ```sql
  ALTER TABLE badges VALIDATE CONSTRAINT fk_badges_users;
  ```

### Connection String Format
PostgreSQL connection strings use Npgsql format:
```
Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=yourpassword
```

Common options:
- `SSL Mode=Require` - Require SSL/TLS encryption
- `Pooling=true` - Enable connection pooling (default)
- `Timeout=30` - Connection timeout in seconds


