using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Vcc.Office.CoreShell;

public sealed record DataColumnDefinition(string Name, string Type, bool Nullable = true, int Position = 0);
public sealed record DataTableDefinition(string Name, string DisplayName, IReadOnlyList<DataColumnDefinition> Columns);
public sealed record DataRelationDefinition(string Name, string ParentTable, string ParentColumn, string ChildTable, string ChildColumn);
public sealed record DataQueryResult(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows);
public sealed record DataFormField(string Name, string Label, string Type = "TEXT", bool Required = false, int Position = 0);
public sealed record DataFormDefinition(string Name, string TableName, string DisplayName, IReadOnlyList<DataFormField> Fields);
public sealed record DataReportDefinition(string Name, string DisplayName, string QuerySql);

public sealed class VccDataService
{
    private readonly CoreDatabase _database;
    private readonly AtomicFileStore _files = new();

    public VccDataService(CoreDatabase database) => _database = database ?? throw new ArgumentNullException(nameof(database));

    public async Task CreateTableAsync(DataTableDefinition definition, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(definition.Name, nameof(definition.Name));
        if (definition.Columns.Count == 0) throw new ArgumentException("A tabela deve ter pelo menos uma coluna.", nameof(definition));
        var columns = definition.Columns.Select((column, index) => column with { Position = index }).ToArray();
        foreach (var column in columns) ValidateIdentifier(column.Name, nameof(column.Name));
        if (columns.Select(column => column.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != columns.Length)
            throw new ArgumentException("Os nomes das colunas devem ser únicos.", nameof(definition));

        await _database.InitialiseAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, transaction, $"INSERT INTO data_tables(name, display_name, created_at) VALUES ($name, $display, $created)", cancellationToken,
            ("$name", definition.Name), ("$display", string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.Name : definition.DisplayName.Trim()), ("$created", DateTimeOffset.UtcNow.ToString("O")));
        foreach (var column in columns)
        {
            var sql = "INSERT INTO data_columns(table_name, name, type, nullable, position) VALUES ($table, $name, $type, $nullable, $position)";
            await ExecuteAsync(connection, transaction, sql, cancellationToken, ("$table", definition.Name), ("$name", column.Name), ("$type", NormalizeType(column.Type)), ("$nullable", column.Nullable ? 1 : 0), ("$position", column.Position));
        }
        await ExecuteAsync(connection, transaction, $"CREATE TABLE {Quote(definition.Name)} ({string.Join(", ", columns.Select(column => $"{Quote(column.Name)} {NormalizeType(column.Type)}{(column.Nullable ? "" : " NOT NULL")}"))})", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DataTableDefinition>> ListTablesAsync(CancellationToken cancellationToken = default)
    {
        await _database.InitialiseAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        var tables = new List<DataTableDefinition>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, display_name FROM data_tables ORDER BY name";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            tables.Add(new DataTableDefinition(reader.GetString(0), reader.GetString(1), []));
        foreach (var table in tables)
        {
            await using var columns = connection.CreateCommand();
            columns.CommandText = "SELECT name, type, nullable, position FROM data_columns WHERE table_name = $table ORDER BY position";
            columns.Parameters.AddWithValue("$table", table.Name);
            await using var columnReader = await columns.ExecuteReaderAsync(cancellationToken);
            var definitions = new List<DataColumnDefinition>();
            while (await columnReader.ReadAsync(cancellationToken)) definitions.Add(new(columnReader.GetString(0), columnReader.GetString(1), columnReader.GetInt32(2) != 0, columnReader.GetInt32(3)));
            tables[tables.IndexOf(table)] = table with { Columns = definitions };
        }
        return tables;
    }

    public async Task InsertRowAsync(string tableName, IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(tableName, nameof(tableName));
        if (values.Count == 0) throw new ArgumentException("A linha deve ter pelo menos um valor.", nameof(values));
        foreach (var name in values.Keys) ValidateIdentifier(name, nameof(values));
        await _database.InitialiseAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var names = values.Keys.ToArray();
        command.CommandText = $"INSERT INTO {Quote(tableName)} ({string.Join(", ", names.Select(Quote))}) VALUES ({string.Join(", ", names.Select((_, index) => "$value" + index))})";
        for (var index = 0; index < names.Length; index++) command.Parameters.AddWithValue("$value" + index, values[names[index]] ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task CreateRelationAsync(DataRelationDefinition relation, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(relation.Name, nameof(relation.Name));
        ValidateIdentifier(relation.ParentTable, nameof(relation.ParentTable));
        ValidateIdentifier(relation.ParentColumn, nameof(relation.ParentColumn));
        ValidateIdentifier(relation.ChildTable, nameof(relation.ChildTable));
        ValidateIdentifier(relation.ChildColumn, nameof(relation.ChildColumn));
        await _database.InitialiseAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, null, "INSERT INTO data_relations(name, parent_table, parent_column, child_table, child_column) VALUES ($name, $parent_table, $parent_column, $child_table, $child_column)", cancellationToken,
            ("$name", relation.Name), ("$parent_table", relation.ParentTable), ("$parent_column", relation.ParentColumn), ("$child_table", relation.ChildTable), ("$child_column", relation.ChildColumn));
    }

    public async Task<int> ImportCsvAsync(string tableName, string csv, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(tableName, nameof(tableName));
        if (string.IsNullOrWhiteSpace(csv)) return 0;
        var records = ParseCsv(csv).ToArray();
        if (records.Length < 2) return 0;
        var headers = records[0];
        if (headers.Length == 0 || headers.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("O CSV deve conter cabeçalhos válidos.", nameof(csv));
        var count = 0;
        foreach (var record in records.Skip(1))
        {
            if (record.Length != headers.Length) throw new ArgumentException("Todas as linhas CSV devem ter o mesmo número de colunas.", nameof(csv));
            await InsertRowAsync(tableName, headers.Select((header, index) => new KeyValuePair<string, object?>(header, record[index])).ToDictionary(pair => pair.Key, pair => pair.Value), cancellationToken);
            count++;
        }
        return count;
    }

    public async Task CreateFormAsync(DataFormDefinition definition, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(definition.Name, nameof(definition.Name));
        ValidateIdentifier(definition.TableName, nameof(definition.TableName));
        if (definition.Fields.Count == 0) throw new ArgumentException("O formulário deve ter pelo menos um campo.", nameof(definition));
        var fields = definition.Fields.Select((field, index) => field with { Position = index, Type = NormalizeType(field.Type) }).ToArray();
        foreach (var field in fields) ValidateIdentifier(field.Name, nameof(field.Name));
        if (fields.Select(field => field.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != fields.Length) throw new ArgumentException("Os campos do formulário devem ser únicos.", nameof(definition));
        await _database.InitialiseAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, transaction, "INSERT INTO data_forms(name, table_name, display_name, created_at) VALUES ($name, $table, $display, $created)", cancellationToken, ("$name", definition.Name), ("$table", definition.TableName), ("$display", string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.Name : definition.DisplayName.Trim()), ("$created", DateTimeOffset.UtcNow.ToString("O")));
        foreach (var field in fields) await ExecuteAsync(connection, transaction, "INSERT INTO data_form_fields(form_name, name, label, type, required, position) VALUES ($form, $name, $label, $type, $required, $position)", cancellationToken, ("$form", definition.Name), ("$name", field.Name), ("$label", string.IsNullOrWhiteSpace(field.Label) ? field.Name : field.Label.Trim()), ("$type", field.Type), ("$required", field.Required ? 1 : 0), ("$position", field.Position));
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DataFormDefinition>> ListFormsAsync(CancellationToken cancellationToken = default)
    {
        await _database.InitialiseAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, table_name, display_name FROM data_forms ORDER BY name";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var forms = new List<DataFormDefinition>();
        while (await reader.ReadAsync(cancellationToken)) forms.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), []));
        for (var formIndex = 0; formIndex < forms.Count; formIndex++)
        {
            var form = forms[formIndex];
            await using var fields = connection.CreateCommand();
            fields.CommandText = "SELECT name, label, type, required, position FROM data_form_fields WHERE form_name = $form ORDER BY position";
            fields.Parameters.AddWithValue("$form", form.Name);
            await using var fieldReader = await fields.ExecuteReaderAsync(cancellationToken);
            var definitions = new List<DataFormField>();
            while (await fieldReader.ReadAsync(cancellationToken)) definitions.Add(new(fieldReader.GetString(0), fieldReader.GetString(1), fieldReader.GetString(2), fieldReader.GetInt32(3) != 0, fieldReader.GetInt32(4)));
            forms[formIndex] = form with { Fields = definitions };
        }
        return forms;
    }

    public async Task SubmitFormAsync(DataFormDefinition definition, IReadOnlyDictionary<string, object?> values, CancellationToken cancellationToken = default)
    {
        var fields = definition.Fields.ToDictionary(field => field.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var field in definition.Fields)
            if (field.Required && (!values.TryGetValue(field.Name, out var value) || value is null || string.IsNullOrWhiteSpace(Convert.ToString(value, CultureInfo.InvariantCulture))))
                throw new ArgumentException($"O campo «{field.Label}» é obrigatório.", nameof(values));
        if (values.Keys.Any(key => !fields.ContainsKey(key))) throw new ArgumentException("O formulário recebeu um campo desconhecido.", nameof(values));
        await InsertRowAsync(definition.TableName, values, cancellationToken);
    }

    public async Task CreateReportAsync(DataReportDefinition definition, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(definition.Name, nameof(definition.Name));
        if (string.IsNullOrWhiteSpace(definition.QuerySql)) throw new ArgumentException("A consulta do relatório é obrigatória.", nameof(definition));
        ValidateReadOnlySql(definition.QuerySql);
        await _database.InitialiseAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, null, "INSERT INTO data_reports(name, display_name, query_sql, created_at) VALUES ($name, $display, $query, $created)", cancellationToken, ("$name", definition.Name), ("$display", string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.Name : definition.DisplayName.Trim()), ("$query", definition.QuerySql.Trim()), ("$created", DateTimeOffset.UtcNow.ToString("O")));
    }

    public async Task<IReadOnlyList<DataReportDefinition>> ListReportsAsync(CancellationToken cancellationToken = default)
    {
        await _database.InitialiseAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, display_name, query_sql FROM data_reports ORDER BY name";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var reports = new List<DataReportDefinition>();
        while (await reader.ReadAsync(cancellationToken)) reports.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        return reports;
    }

    public async Task<DataQueryResult> RunReportAsync(DataReportDefinition definition, CancellationToken cancellationToken = default) => await QueryAsync(definition.QuerySql, cancellationToken);

    public async Task<DataQueryResult> QueryAsync(string sql, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("A consulta é obrigatória.", nameof(sql));
        var trimmed = sql.TrimStart();
        ValidateReadOnlySql(sql);
        await _database.InitialiseAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in columns) row[column] = await reader.IsDBNullAsync(reader.GetOrdinal(column), cancellationToken) ? null : reader.GetValue(reader.GetOrdinal(column));
            rows.Add(row);
        }
        return new DataQueryResult(columns, rows);
    }

    public async Task<string> ExportCsvAsync(string sql, CancellationToken cancellationToken = default)
    {
        var result = await QueryAsync(sql, cancellationToken);
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", result.Columns.Select(EscapeCsv)));
        foreach (var row in result.Rows) builder.AppendLine(string.Join(",", result.Columns.Select(column => EscapeCsv(row[column]))));
        return builder.ToString();
    }

    public async Task<string> BackupAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destinationPath)) throw new ArgumentException("O destino é obrigatório.", nameof(destinationPath));
        await _database.InitialiseAsync(cancellationToken);
        await _files.WriteAsync(destinationPath, await File.ReadAllBytesAsync(_database.DatabasePath, cancellationToken), cancellationToken);
        return Path.GetFullPath(destinationPath);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection($"Data Source={_database.DatabasePath};Pooling=False");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction? transaction, string sql, CancellationToken cancellationToken, params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string NormalizeType(string type) => type.Trim().ToUpperInvariant() switch { "INTEGER" or "REAL" or "TEXT" or "BLOB" or "NUMERIC" => type.Trim().ToUpperInvariant(), _ => throw new ArgumentException("Tipo SQLite inválido. Use INTEGER, REAL, TEXT, BLOB ou NUMERIC.", nameof(type)) };
    private static void ValidateReadOnlySql(string sql)
    {
        var trimmed = sql.TrimStart();
        if (!(trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("EXPLAIN", StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("A consulta deve ser somente de leitura.", nameof(sql));
    }
    private static string Quote(string identifier) => '"' + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    private static void ValidateIdentifier(string identifier, string parameterName) { if (string.IsNullOrWhiteSpace(identifier) || identifier.Any(character => !(char.IsLetterOrDigit(character) || character == '_'))) throw new ArgumentException("O identificador só pode conter letras, números e underscore.", parameterName); }
    private static string EscapeCsv(object? value) { var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty; return text.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? '"' + text.Replace("\"", "\"\"", StringComparison.Ordinal) + '"' : text; }
    private static IEnumerable<string[]> ParseCsv(string csv)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var value = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < csv.Length; index++)
        {
            var character = csv[index];
            if (character == '"')
            {
                if (quoted && index + 1 < csv.Length && csv[index + 1] == '"') { value.Append('"'); index++; }
                else quoted = !quoted;
            }
            else if (character == ',' && !quoted) { row.Add(value.ToString()); value.Clear(); }
            else if ((character == '\r' || character == '\n') && !quoted)
            {
                if (character == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n') index++;
                row.Add(value.ToString()); value.Clear();
                if (row.Any(cell => cell.Length > 0)) rows.Add(row.ToArray());
                row.Clear();
            }
            else value.Append(character);
        }
        if (quoted) throw new ArgumentException("O CSV contém aspas não fechadas.", nameof(csv));
        if (value.Length > 0 || row.Count > 0) { row.Add(value.ToString()); rows.Add(row.ToArray()); }
        return rows;
    }
}
