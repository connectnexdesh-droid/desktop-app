using Microsoft.Data.Sqlite;
using QuickDock.Models;

namespace QuickDock.Services;

public sealed class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QuickDock");
        Directory.CreateDirectory(folder);

        var databasePath = Path.Combine(folder, "quickdock.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;

            CREATE TABLE IF NOT EXISTS TextRows (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SheetName TEXT NOT NULL,
                Position INTEGER NOT NULL,
                Text TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_TextRows_Sheet_Position
                ON TextRows(SheetName, Position);

            CREATE TABLE IF NOT EXISTS MainRows (
                RowNumber INTEGER PRIMARY KEY,
                A TEXT NOT NULL DEFAULT '',
                B TEXT NOT NULL DEFAULT '',
                C TEXT NOT NULL DEFAULT '',
                D TEXT NOT NULL DEFAULT '',
                E TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS AppSettings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public List<TextRow> LoadTextRows(string sheetName)
    {
        var rows = new List<TextRow>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Position, Text
            FROM TextRows
            WHERE SheetName = $sheet
            ORDER BY Position;
            """;
        command.Parameters.AddWithValue("$sheet", sheetName);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new TextRow
            {
                RowNumber = reader.GetInt32(0),
                Text = reader.GetString(1)
            });
        }

        return rows;
    }

    public void SaveTextRows(string sheetName, IEnumerable<TextRow> rows)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM TextRows WHERE SheetName = $sheet;";
            delete.Parameters.AddWithValue("$sheet", sheetName);
            delete.ExecuteNonQuery();
        }

        var position = 1;
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Text))
                continue;

            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO TextRows (SheetName, Position, Text)
                VALUES ($sheet, $position, $text);
                """;
            insert.Parameters.AddWithValue("$sheet", sheetName);
            insert.Parameters.AddWithValue("$position", position++);
            insert.Parameters.AddWithValue("$text", row.Text);
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public List<SheetRow> LoadMainRows()
    {
        var rows = new List<SheetRow>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT RowNumber, A, B, C, D, E
            FROM MainRows
            ORDER BY RowNumber;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new SheetRow
            {
                RowNumber = reader.GetInt32(0),
                A = reader.GetString(1),
                B = reader.GetString(2),
                C = reader.GetString(3),
                D = reader.GetString(4),
                E = reader.GetString(5)
            });
        }

        return rows;
    }

    public void SaveMainRows(IEnumerable<SheetRow> rows)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM MainRows;";
            delete.ExecuteNonQuery();
        }

        foreach (var row in rows)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO MainRows (RowNumber, A, B, C, D, E)
                VALUES ($row, $a, $b, $c, $d, $e);
                """;
            insert.Parameters.AddWithValue("$row", row.RowNumber);
            insert.Parameters.AddWithValue("$a", row.A ?? string.Empty);
            insert.Parameters.AddWithValue("$b", row.B ?? string.Empty);
            insert.Parameters.AddWithValue("$c", row.C ?? string.Empty);
            insert.Parameters.AddWithValue("$d", row.D ?? string.Empty);
            insert.Parameters.AddWithValue("$e", row.E ?? string.Empty);
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public AppSettings LoadSettings()
    {
        var settings = new AppSettings();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Key, Value FROM AppSettings;";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var key = reader.GetString(0);
            var value = reader.GetString(1).Equals("true", StringComparison.OrdinalIgnoreCase);

            switch (key)
            {
                case nameof(AppSettings.FloatingEnabled): settings.FloatingEnabled = value; break;
                case nameof(AppSettings.Txt1Enabled): settings.Txt1Enabled = value; break;
                case nameof(AppSettings.Txt2Enabled): settings.Txt2Enabled = value; break;
                case nameof(AppSettings.Txt3Enabled): settings.Txt3Enabled = value; break;
                case nameof(AppSettings.Txt4Enabled): settings.Txt4Enabled = value; break;
                case nameof(AppSettings.TwoFaEnabled): settings.TwoFaEnabled = value; break;
                case nameof(AppSettings.AEnabled): settings.AEnabled = value; break;
                case nameof(AppSettings.BEnabled): settings.BEnabled = value; break;
                case nameof(AppSettings.CEnabled): settings.CEnabled = value; break;
                case nameof(AppSettings.DEnabled): settings.DEnabled = value; break;
                case nameof(AppSettings.EEnabled): settings.EEnabled = value; break;
            }
        }

        return settings;
    }

    public void SaveSettings(AppSettings settings)
    {
        var values = new Dictionary<string, bool>
        {
            [nameof(AppSettings.FloatingEnabled)] = settings.FloatingEnabled,
            [nameof(AppSettings.Txt1Enabled)] = settings.Txt1Enabled,
            [nameof(AppSettings.Txt2Enabled)] = settings.Txt2Enabled,
            [nameof(AppSettings.Txt3Enabled)] = settings.Txt3Enabled,
            [nameof(AppSettings.Txt4Enabled)] = settings.Txt4Enabled,
            [nameof(AppSettings.TwoFaEnabled)] = settings.TwoFaEnabled,
            [nameof(AppSettings.AEnabled)] = settings.AEnabled,
            [nameof(AppSettings.BEnabled)] = settings.BEnabled,
            [nameof(AppSettings.CEnabled)] = settings.CEnabled,
            [nameof(AppSettings.DEnabled)] = settings.DEnabled,
            [nameof(AppSettings.EEnabled)] = settings.EEnabled
        };

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var pair in values)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO AppSettings (Key, Value)
                VALUES ($key, $value)
                ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
                """;
            command.Parameters.AddWithValue("$key", pair.Key);
            command.Parameters.AddWithValue("$value", pair.Value ? "true" : "false");
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }
}
