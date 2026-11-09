
using Npgsql;

namespace LibraryManagementApp.MongoDb;

public sealed class PostgresBookCatalogReader
{
    private readonly string _connectionString;

    public PostgresBookCatalogReader(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<BookCatalogDocument>> ReadBooksAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                b.id,
                b.isbn,
                b.title,
                b.publication_year,
                b.language,
                g.name AS genre,
                p.name AS publisher,

                COALESCE(
                    (
                        SELECT jsonb_agg(
                            a.first_name || ' ' || a.last_name
                            ORDER BY a.last_name, a.first_name
                        )
                        FROM book_authors ba
                        JOIN authors a ON a.id = ba.author_id
                        WHERE ba.book_id = b.id
                          AND a.is_deleted = false
                    ),
                    '[]'::jsonb
                )::text AS authors_json,

                COALESCE(
                    (
                        SELECT jsonb_agg(
                            jsonb_build_object(
                                'id', bc.id,
                                'barcode', bc.barcode,
                                'branchId', br.id,
                                'branchName', br.name,
                                'status', bc.status
                            )
                            ORDER BY bc.id
                        )
                        FROM book_copies bc
                        JOIN branches br ON br.id = bc.branch_id
                        WHERE bc.book_id = b.id
                          AND br.is_deleted = false
                    ),
                    '[]'::jsonb
                )::text AS copies_json

            FROM books b
            JOIN genres g ON g.id = b.genre_id
            JOIN publishers p ON p.id = b.publisher_id
            WHERE b.is_deleted = false
            ORDER BY b.id;
            """;

        var result = new List<BookCatalogDocument>();

        await using var connection =
            new NpgsqlConnection(_connectionString);

        await connection.OpenAsync(cancellationToken);

        await using var command =
            new NpgsqlCommand(sql, connection);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var authorsJson = reader.GetString(7);
            var copiesJson = reader.GetString(8);

            var authors =
                System.Text.Json.JsonSerializer.Deserialize<List<string>>(
                    authorsJson) ?? [];

            var copies =
                System.Text.Json.JsonSerializer.Deserialize<List<BookCopyDocument>>(
                    copiesJson,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? [];

            var book = new BookCatalogDocument
            {
                Id = reader.GetInt32(0),
                Isbn = reader.GetString(1),
                Title = reader.GetString(2),
                PublicationYear = reader.IsDBNull(3)
                    ? null
                    : reader.GetInt32(3),
                Language = reader.IsDBNull(4)
                    ? null
                    : reader.GetString(4),
                Genre = reader.GetString(5),
                Publisher = reader.GetString(6),
                Authors = authors,
                Copies = copies,
                TotalCopies = copies.Count,
                AvailableCopies = copies.Count(
                    x => x.Status == "available")
            };

            result.Add(book);
        }

        return result;
    }
}
