# DotNet-Soddi (Stack Overflow Data Dump Importer)

Inspirited by the original [Soddi](https://github.com/BrentOzarULTD/soddi), DotNet-Soddi is a console application 
that assists in not just importing the Stack Overflow data dumps, but also obtaining them.

## Features

- Discover archives from the command line.
- Importing data from xml files or stream directly from the .7z archive files.
- Download archive files from the command line via HTTP based on their site name.
- Download archive files via BitTorrent based on their site name.

## Limitations

Not all features of Soddi are supported.

- It's only for SQL Server.
- Full Text isn't supported.

## Download and install a database in two lines

The following command will download the archive for the math.stackexchange.com, then import it into an existing 
database named math.stackexchange.com

```bash
soddi download math
soddi import math -d math.stackexchange.com
```

Because of the size of the database and the bandwidth of archive.org, you might be better off using the torrent 
option. The following command will do everything required to connect to the appropriate trackers and peers to download
the math database and exit upon completion.

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

  -o, --output        Output folder
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

Import data using defaults:
  soddi import math.stackexchange.com.7z
Import data using a connection string and database name:
  soddi import --connectionString "Server=(local)\Sql2017;User Id=admin;password=t3ddy" --database math
  math.stackexchange.com.7z
Import data using defaults and create database:
  soddi import --dropAndCreate math.stackexchange.com.7z
Import data using defaults without constraints:
  soddi import --skipConstraints math.stackexchange.com.7z

  -d, --database            Name of database. If omitted the name of the file or folder will be used.
  -c, --connectionString    (Default: Server=.;Integrated Security=true) Connection string to server. Initial catalog
                            will be ignored and the database parameter will be used for the database name.
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

Experimental. Download database via BitTorrent. For larger databases this might prove significantly faster.

```bash
USAGE:

Download files associated with the math site from the torrent file:
  soddi torrent math
Download to a specific folder:
  soddi torrent --output "c:\torrent files" math
Enable port forwarding:
  soddi torrent --portForward math

  -o, --output         Output folder
  -f, --portForward    (Default: false) Experimental. Enable port forwarding
  --help               Display this help screen.
  --version            Display version information.
  Archive (pos. 0)     Required. Archive to download
  ```
  