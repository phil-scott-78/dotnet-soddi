﻿using Microsoft.Data.SqlClient;

namespace Soddi.Tasks.SqlServer;

public class VerifyDatabaseExists(string connectionString, string databaseName) : ITask
{
    public async Task GoAsync(IProgress<(string taskId, string message, double weight, double maxValue)> progress, CancellationToken cancellationToken)
    {
        var sql = $"select COUNT(*) from sys.databases where name = '{databaseName}'";
        await using var sqlConn = new SqlConnection(connectionString);
        await using var sqlCommand = new SqlCommand(sql, sqlConn);

        await sqlConn.OpenAsync(cancellationToken);
        progress.Report(("createDb", "Creating database", GetTaskWeight() / 2, GetTaskWeight()));
        var result = (int)await sqlCommand.ExecuteScalarAsync(cancellationToken);
        if (result == 0)
        {
            throw new SoddiException(
                $"Database {databaseName} does not exists.\nDatabase must exist, or use the --dropAndCreate option to build a default database.");
        }
    }

    public double GetTaskWeight()
    {
        return 100;
    }
}

public class CreateDatabase(string connectionString, string databaseName) : ITask
{
    public async Task GoAsync(IProgress<(string taskId, string message, double weight, double maxValue)> progress, CancellationToken cancellationToken)
    {
        var statements = Sql.Replace("DummyDatabaseName", databaseName).Split("GO");
        await using var sqlConn = new SqlConnection(connectionString);
        await sqlConn.OpenAsync(cancellationToken);

        var incrementValue = GetTaskWeight() / statements.Length;
        foreach (var statement in statements)
        {
            progress.Report(("createDb", "Creating database", incrementValue, GetTaskWeight()));
            await using var command = new SqlCommand(statement, sqlConn);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public double GetTaskWeight()
    {
        return 10000;
    }

    private const string Sql = @"
USE [master]
GO

IF EXISTS(select NULL from sys.databases where name = 'DummyDatabaseName' )
    ALTER DATABASE [DummyDatabaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE
GO

IF EXISTS(select NULL from sys.databases where name = 'DummyDatabaseName' )
    DROP DATABASE [DummyDatabaseName]
GO

CREATE DATABASE [DummyDatabaseName]
GO

ALTER DATABASE [DummyDatabaseName] SET ANSI_NULL_DEFAULT OFF 
GO

ALTER DATABASE [DummyDatabaseName] SET ANSI_NULLS OFF 
GO

ALTER DATABASE [DummyDatabaseName] SET ANSI_PADDING OFF 
GO

ALTER DATABASE [DummyDatabaseName] SET ANSI_WARNINGS OFF 
GO

ALTER DATABASE [DummyDatabaseName] SET ARITHABORT OFF 
GO

ALTER DATABASE [DummyDatabaseName] SET AUTO_CLOSE OFF 
GO

ALTER DATABASE [DummyDatabaseName] SET AUTO_SHRINK OFF 
GO

ALTER DATABASE [DummyDatabaseName] SET AUTO_UPDATE_STATISTICS ON 
GO

ALTER DATABASE [DummyDatabaseName] SET CURSOR_CLOSE_ON_COMMIT OFF 
GO

ALTER DATABASE [DummyDatabaseName] SET CURSOR_DEFAULT  GLOBAL 
GO

ALTER DATABASE [DummyDatabaseName] SET CONCAT_NULL_YIELDS_NULL OFF 
GO

ALTER DATABASE [DummyDatabaseName] SET NUMERIC_ROUNDABORT OFF 
GO

ALTER DATABASE [DummyDatabaseName] SET QUOTED_IDENTIFIER OFF 
GO

ALTER DATABASE [DummyDatabaseName] SET RECURSIVE_TRIGGERS OFF 
GO

ALTER DATABASE [DummyDatabaseName] SET DISABLE_BROKER 
GO

ALTER DATABASE [DummyDatabaseName] SET AUTO_UPDATE_STATISTICS_ASYNC OFF 
GO

ALTER DATABASE [DummyDatabaseName] SET DATE_CORRELATION_OPTIMIZATION OFF 
GO

ALTER DATABASE [DummyDatabaseName] SET TRUSTWORTHY OFF 
GO

ALTER DATABASE [DummyDatabaseName] SET ALLOW_SNAPSHOT_ISOLATION OFF 
GO

ALTER DATABASE [DummyDatabaseName] SET PARAMETERIZATION SIMPLE 
GO

ALTER DATABASE [DummyDatabaseName] SET READ_COMMITTED_SNAPSHOT OFF 
GO

ALTER DATABASE [DummyDatabaseName] SET HONOR_BROKER_PRIORITY OFF 
GO

ALTER DATABASE [DummyDatabaseName] SET RECOVERY SIMPLE 
GO

ALTER DATABASE [DummyDatabaseName] SET  MULTI_USER 
GO

ALTER DATABASE [DummyDatabaseName] SET PAGE_VERIFY CHECKSUM  
GO

ALTER DATABASE [DummyDatabaseName] SET DB_CHAINING OFF 
GO

ALTER DATABASE [DummyDatabaseName] SET FILESTREAM( NON_TRANSACTED_ACCESS = OFF ) 
GO

ALTER DATABASE [DummyDatabaseName] SET TARGET_RECOVERY_TIME = 60 SECONDS 
GO

ALTER DATABASE [DummyDatabaseName] SET DELAYED_DURABILITY = DISABLED 
GO

ALTER DATABASE [DummyDatabaseName] SET QUERY_STORE = ON (OPERATION_MODE = READ_WRITE)
GO

ALTER DATABASE [DummyDatabaseName] SET  READ_WRITE 
GO
";
}
