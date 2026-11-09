
using System.Diagnostics;
using System.Globalization;
using System.Text;
using LibraryManagementApp.MongoDb;
using MongoDB.Driver;
using Npgsql;

namespace LibraryManagementApp.Benchmark;

public sealed class Lab2Benchmark
{
    private const int BookCount = 10_000;
    private const int Repetitions = 100;
    private const int Rounds = 5;

    private readonly string _postgresConnection;
    private readonly IMongoCollection<BookCatalogDocument> _mongo;

    private readonly List<BenchmarkResult> _results = [];

    private static readonly string[] Genres =
    [
        "Fantasy",
        "History",
        "Science",
        "Fiction",
        "Education"
    ];

    public Lab2Benchmark(
        string postgresConnection,
        string mongoConnection)
    {
        _postgresConnection = postgresConnection;

        var mongoClient = new MongoClient(mongoConnection);

        _mongo = mongoClient
            .GetDatabase("LibraryNoSqlDb")
            .GetCollection<BookCatalogDocument>("benchmark_books");
    }

    public async Task RunAsync()
    {
        Console.WriteLine();
        Console.WriteLine("========== РОЗШИРЕНЕ ТЕСТУВАННЯ ==========");
        Console.WriteLine($"Кількість книг: {BookCount}");
        Console.WriteLine($"Серій вимірювань: {Rounds}");
        Console.WriteLine($"Операцій у серії: {Repetitions}");
        Console.WriteLine();

        await using var postgres =
            new NpgsqlConnection(_postgresConnection);

        await postgres.OpenAsync();

        Console.WriteLine("Підготовка PostgreSQL...");
        await PreparePostgresAsync(postgres);

        Console.WriteLine("Підготовка MongoDB...");
        await PrepareMongoAsync();

        long postgresCount = await GetPostgresCountAsync(postgres);

        long mongoCount = await _mongo.CountDocumentsAsync(
            Builders<BookCatalogDocument>.Filter.Empty);

        Console.WriteLine($"PostgreSQL: {postgresCount}");
        Console.WriteLine($"MongoDB: {mongoCount}");

        if (postgresCount != BookCount || mongoCount != BookCount)
        {
            throw new InvalidOperationException(
                "Кількість книг у базах відрізняється.");
        }

        Console.WriteLine();
        Console.WriteLine("Перевірка відповідності результатів...");

        for (int id = 1; id <= BookCount; id += 499)
        {
            var pgBook = await FindPostgresBookAsync(postgres, id);
            var mongoBook = await FindMongoBookAsync(id);

            if (pgBook is null ||
                mongoBook is null ||
                pgBook.Value.Title != mongoBook.Title ||
                pgBook.Value.Author != mongoBook.Authors.Single() ||
                pgBook.Value.Copies != mongoBook.Copies.Count)
            {
                throw new InvalidOperationException(
                    $"Дані книги {id} не збігаються.");
            }
        }

        foreach (string genre in Genres)
        {
            long pgCount =
                await CountPostgresGenreAsync(postgres, genre);

            long mongoGenreCount =
                await CountMongoGenreAsync(genre);

            if (pgCount != mongoGenreCount)
            {
                throw new InvalidOperationException(
                    $"Кількість книг жанру {genre} відрізняється.");
            }

            var pgPage =
                await GetPostgresGenrePageAsync(postgres, genre);

            var mongoPage =
                await GetMongoGenrePageAsync(genre);

            if (!pgPage.SequenceEqual(mongoPage))
            {
                throw new InvalidOperationException(
                    $"Вибірки жанру {genre} не збігаються.");
            }
        }

        Console.WriteLine("Відповідність даних підтверджена.");

        Console.WriteLine();
        Console.WriteLine("Прогрів запитів...");

        for (int i = 0; i < 30; i++)
        {
            int id = GetBookId(i);
            string genre = GetGenre(i);

            await FindPostgresBookAsync(postgres, id);
            await FindMongoBookAsync(id);

            await CountPostgresGenreAsync(postgres, genre);
            await CountMongoGenreAsync(genre);

            await GetPostgresGenrePageAsync(postgres, genre);
            await GetMongoGenrePageAsync(genre);
        }

        _results.Clear();

        await CompareAsync(
            "Повна картка книги",
            i => FindPostgresBookAsTask(postgres, GetBookId(i)),
            i => FindMongoBookAsTask(GetBookId(i)));

        await CompareAsync(
            "Підрахунок за жанром",
            async i =>
            {
                _ = await CountPostgresGenreAsync(
                    postgres, GetGenre(i));
            },
            async i =>
            {
                _ = await CountMongoGenreAsync(GetGenre(i));
            });

        await CompareAsync(
            "20 книг за жанром",
            async i =>
            {
                _ = await GetPostgresGenrePageAsync(
                    postgres, GetGenre(i));
            },
            async i =>
            {
                _ = await GetMongoGenrePageAsync(GetGenre(i));
            });

        await SaveResultsAsync();

        Console.WriteLine();
        Console.WriteLine("========== ТЕСТУВАННЯ ЗАВЕРШЕНО ==========");
        Console.WriteLine("Створено файл lab2_benchmark.csv");
    }

    private static int GetBookId(int iteration)
    {
        return (iteration * 997) % BookCount + 1;
    }

    private static string GetGenre(int value)
    {
        return Genres[value % Genres.Length];
    }

    private async Task CompareAsync(
        string operation,
        Func<int, Task> postgresOperation,
        Func<int, Task> mongoOperation)
    {
        Console.WriteLine();
        Console.WriteLine($"--- {operation} ---");

        for (int round = 1; round <= Rounds; round++)
        {
            double postgresMs;
            double mongoMs;

            // Чергуємо порядок запуску, щоб зменшити
            // вплив прогріву на одну конкретну СУБД.
            if (round % 2 == 1)
            {
                postgresMs = await MeasureAsync(postgresOperation);
                mongoMs = await MeasureAsync(mongoOperation);
            }
            else
            {
                mongoMs = await MeasureAsync(mongoOperation);
                postgresMs = await MeasureAsync(postgresOperation);
            }

            _results.Add(
                new BenchmarkResult(
                    operation,
                    round,
                    postgresMs,
                    mongoMs));

            Console.WriteLine(
                $"Серія {round}: " +
                $"PostgreSQL = {postgresMs:F4} мс; " +
                $"MongoDB = {mongoMs:F4} мс");
        }

        var operationResults = _results
            .Where(x => x.Operation == operation)
            .ToList();

        double postgresMedian = Median(
            operationResults.Select(x => x.PostgresMs));

        double mongoMedian = Median(
            operationResults.Select(x => x.MongoMs));

        Console.WriteLine();
        Console.WriteLine(
            $"Медіана PostgreSQL: {postgresMedian:F4} мс");

        Console.WriteLine(
            $"Медіана MongoDB: {mongoMedian:F4} мс");

        string winner = postgresMedian < mongoMedian
            ? "PostgreSQL"
            : mongoMedian < postgresMedian
                ? "MongoDB"
                : "Однаково";

        Console.WriteLine($"Швидша система: {winner}");
    }

    private static async Task<double> MeasureAsync(
        Func<int, Task> operation)
    {
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < Repetitions; i++)
        {
            await operation(i);
        }

        stopwatch.Stop();

        return stopwatch.Elapsed.TotalMilliseconds / Repetitions;
    }

    private static double Median(IEnumerable<double> values)
    {
        double[] sorted = values.OrderBy(x => x).ToArray();

        if (sorted.Length == 0)
        {
            throw new InvalidOperationException(
                "Немає результатів вимірювання.");
        }

        int middle = sorted.Length / 2;

        if (sorted.Length % 2 == 1)
        {
            return sorted[middle];
        }

        return (sorted[middle - 1] + sorted[middle]) / 2;
    }

    private static async Task PreparePostgresAsync(
        NpgsqlConnection connection)
    {
        const string sql = """
            CREATE SCHEMA IF NOT EXISTS lab2_benchmark;

            CREATE TABLE IF NOT EXISTS lab2_benchmark.authors (
                id integer PRIMARY KEY,
                name text NOT NULL
            );

            CREATE TABLE IF NOT EXISTS lab2_benchmark.books (
                id integer PRIMARY KEY,
                isbn text NOT NULL,
                title text NOT NULL,
                genre text NOT NULL
            );

            CREATE TABLE IF NOT EXISTS lab2_benchmark.book_authors (
                book_id integer NOT NULL
                    REFERENCES lab2_benchmark.books(id),
                author_id integer NOT NULL
                    REFERENCES lab2_benchmark.authors(id),
                PRIMARY KEY (book_id, author_id)
            );

            CREATE TABLE IF NOT EXISTS lab2_benchmark.book_copies (
                id integer PRIMARY KEY,
                book_id integer NOT NULL
                    REFERENCES lab2_benchmark.books(id),
                status text NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_lab2_books_genre_id
                ON lab2_benchmark.books(genre, id);

            CREATE INDEX IF NOT EXISTS idx_lab2_copies_book_id
                ON lab2_benchmark.book_copies(book_id);

            TRUNCATE
                lab2_benchmark.book_authors,
                lab2_benchmark.book_copies,
                lab2_benchmark.books,
                lab2_benchmark.authors;

            INSERT INTO lab2_benchmark.authors(id, name)
            SELECT id, 'Author ' || id
            FROM generate_series(1, 10000) AS id;

            INSERT INTO lab2_benchmark.books(id, isbn, title, genre)
            SELECT
                id,
                'ISBN-' || id,
                'Book ' || id,
                CASE id % 5
                    WHEN 0 THEN 'Fantasy'
                    WHEN 1 THEN 'History'
                    WHEN 2 THEN 'Science'
                    WHEN 3 THEN 'Fiction'
                    ELSE 'Education'
                END
            FROM generate_series(1, 10000) AS id;

            INSERT INTO lab2_benchmark.book_authors(
                book_id, author_id)
            SELECT id, id
            FROM generate_series(1, 10000) AS id;

            INSERT INTO lab2_benchmark.book_copies(
                id, book_id, status)
            SELECT
                (b.id - 1) * 2 + n,
                b.id,
                CASE WHEN n = 1
                    THEN 'available'
                    ELSE 'borrowed'
                END
            FROM lab2_benchmark.books b
            CROSS JOIN generate_series(1, 2) AS n;

            ANALYZE lab2_benchmark.books;
            ANALYZE lab2_benchmark.authors;
            ANALYZE lab2_benchmark.book_authors;
            ANALYZE lab2_benchmark.book_copies;
            """;

        await using var transaction =
            await connection.BeginTransactionAsync();

        await using var command =
            new NpgsqlCommand(sql, connection, transaction);

        await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
    }

    private async Task PrepareMongoAsync()
    {
        await _mongo.DeleteManyAsync(
            Builders<BookCatalogDocument>.Filter.Empty);

        var documents = new List<BookCatalogDocument>(1000);

        for (int id = 1; id <= BookCount; id++)
        {
            string genre = (id % 5) switch
            {
                0 => "Fantasy",
                1 => "History",
                2 => "Science",
                3 => "Fiction",
                _ => "Education"
            };

            documents.Add(new BookCatalogDocument
            {
                Id = id,
                Isbn = $"ISBN-{id}",
                Title = $"Book {id}",
                Genre = genre,
                Publisher = "Benchmark Publisher",
                Authors = [$"Author {id}"],
                Copies =
                [
                    new BookCopyDocument
                    {
                        Id = (id - 1) * 2 + 1,
                        Barcode = $"BC-{id}-1",
                        BranchId = 1,
                        BranchName = "Main Branch",
                        Status = "available"
                    },
                    new BookCopyDocument
                    {
                        Id = (id - 1) * 2 + 2,
                        Barcode = $"BC-{id}-2",
                        BranchId = 1,
                        BranchName = "Main Branch",
                        Status = "borrowed"
                    }
                ],
                TotalCopies = 2,
                AvailableCopies = 1
            });

            if (documents.Count == 1000)
            {
                await _mongo.InsertManyAsync(documents);
                documents.Clear();
            }
        }

        if (documents.Count > 0)
        {
            await _mongo.InsertManyAsync(documents);
        }

        var index = new CreateIndexModel<BookCatalogDocument>(
            Builders<BookCatalogDocument>.IndexKeys
                .Ascending(x => x.Genre)
                .Ascending(x => x.Id));

        await _mongo.Indexes.CreateOneAsync(index);
    }

    private static async Task<long> GetPostgresCountAsync(
        NpgsqlConnection connection)
    {
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM lab2_benchmark.books",
            connection);

        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static async Task<(string Title, string Author, int Copies)?>
        FindPostgresBookAsync(
            NpgsqlConnection connection,
            int id)
    {
        const string sql = """
            SELECT b.title, a.name, COUNT(c.id)
            FROM lab2_benchmark.books b
            JOIN lab2_benchmark.book_authors ba
                ON ba.book_id = b.id
            JOIN lab2_benchmark.authors a
                ON a.id = ba.author_id
            LEFT JOIN lab2_benchmark.book_copies c
                ON c.book_id = b.id
            WHERE b.id = @id
            GROUP BY b.id, b.title, a.name;
            """;

        await using var command =
            new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("id", id);

        await using var reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return (
            reader.GetString(0),
            reader.GetString(1),
            checked((int)reader.GetInt64(2))
        );
    }

    private async Task<BookCatalogDocument?> FindMongoBookAsync(
        int id)
    {
        return await _mongo
            .Find(x => x.Id == id)
            .FirstOrDefaultAsync();
    }

    private static async Task<long> CountPostgresGenreAsync(
        NpgsqlConnection connection,
        string genre)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM lab2_benchmark.books
            WHERE genre = @genre;
            """;

        await using var command =
            new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("genre", genre);

        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private async Task<long> CountMongoGenreAsync(
        string genre)
    {
        return await _mongo.CountDocumentsAsync(
            x => x.Genre == genre);
    }

    private static async Task<List<BookListItem>>
        GetPostgresGenrePageAsync(
            NpgsqlConnection connection,
            string genre)
    {
        const string sql = """
            SELECT id, title
            FROM lab2_benchmark.books
            WHERE genre = @genre
            ORDER BY id
            LIMIT 20;
            """;

        var result = new List<BookListItem>();

        await using var command =
            new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue("genre", genre);

        await using var reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new BookListItem
            {
                Id = reader.GetInt32(0),
                Title = reader.GetString(1)
            });
        }

        return result;
    }

    private async Task<List<BookListItem>>
        GetMongoGenrePageAsync(string genre)
    {
        return await _mongo
            .Find(x => x.Genre == genre)
            .SortBy(x => x.Id)
            .Limit(20)
            .Project(x => new BookListItem
            {
                Id = x.Id,
                Title = x.Title
            })
            .ToListAsync();
    }

    private static async Task FindPostgresBookAsTask(
        NpgsqlConnection connection,
        int id)
    {
        _ = await FindPostgresBookAsync(connection, id);
    }

    private async Task FindMongoBookAsTask(int id)
    {
        _ = await FindMongoBookAsync(id);
    }

    private async Task SaveResultsAsync()
    {
        var csv = new StringBuilder();

        csv.AppendLine(
            "Operation;Round;PostgreSQL_ms;MongoDB_ms;Books;Repetitions");

        foreach (var result in _results)
        {
            csv.AppendLine(
                $"{result.Operation};" +
                $"{result.Round};" +
                $"{result.PostgresMs.ToString("F4", CultureInfo.InvariantCulture)};" +
                $"{result.MongoMs.ToString("F4", CultureInfo.InvariantCulture)};" +
                $"{BookCount};" +
                $"{Repetitions}");
        }

        await File.WriteAllTextAsync(
            "lab2_benchmark.csv",
            csv.ToString(),
            Encoding.UTF8);
    }

    private sealed class BookListItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;

        public override bool Equals(object? obj)
        {
            return obj is BookListItem other &&
                   Id == other.Id &&
                   Title == other.Title;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Title);
        }
    }

    private sealed record BenchmarkResult(
        string Operation,
        int Round,
        double PostgresMs,
        double MongoMs);
}
