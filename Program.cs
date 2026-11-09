using Microsoft.Extensions.Configuration;
using LibraryManagementApp.Benchmark;
using LibraryManagementApp.MongoDb;
using LibraryManagementApp.Redis;
using LibraryManagementApp.UnitOfWork;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

var configuration =
    new Microsoft.Extensions.Configuration.ConfigurationBuilder()
        .AddUserSecrets<Program>()
        .Build();

string connectionString =
    configuration.GetSection("ConnectionStrings")["PostgreSQL"]
    ?? throw new InvalidOperationException(
        "Не знайдено рядок підключення PostgreSQL у User Secrets.");

string mongoConnectionString =
    "mongodb://localhost:27017";

string redisConnectionString =
    "localhost:6379";

// ==========================================
// ГОЛОВНЕ МЕНЮ ПРОГРАМИ
// ==========================================

while (true)
{
    Console.WriteLine();
    Console.WriteLine("======================================");
    Console.WriteLine("     LIBRARY MANAGEMENT SYSTEM");
    Console.WriteLine("======================================");
    Console.WriteLine();
    Console.WriteLine("1 - ЛР №1: PostgreSQL + Unit of Work");
    Console.WriteLine("2 - ЛР №2: PostgreSQL -> MongoDB");
    Console.WriteLine("3 - ЛР №2: Порівняння швидкодії");
    Console.WriteLine("4 - ЛР №2: Redis кешування");
    Console.WriteLine();
    Console.WriteLine("0 - Вийти з програми");
    Console.WriteLine();
    Console.Write("Оберіть дію (0-4): ");

    string? choice = Console.ReadLine();

    if (choice == "0" || choice is null)
    {
        Console.WriteLine();
        Console.WriteLine("Завершення роботи програми...");
        break;
    }

    try
    {
        switch (choice)
        {
            case "1":
                await RunLab1Async();
                break;

            case "2":
                await RunMongoSyncAsync();
                break;

            case "3":
                await RunBenchmarkAsync();
                break;

            case "4":
                await RunRedisDemoAsync();
                break;

            default:
                Console.WriteLine();
                Console.WriteLine(
                    "Невідомий пункт меню. Спробуйте ще раз.");
                continue;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine();
        Console.WriteLine("Помилка виконання:");
        Console.WriteLine(ex.ToString());
    }

    Console.WriteLine();
    Console.WriteLine("--------------------------------------");
    Console.WriteLine("Операцію завершено.");
    Console.WriteLine("Натисніть Enter для повернення в меню...");
    Console.ReadLine();

    Console.Clear();
}

// ==========================================
// ЛР №2 — MONGODB
// ==========================================

async Task RunMongoSyncAsync()
{
    Console.WriteLine();
    Console.WriteLine("========== MONGODB ==========");
    Console.WriteLine("Синхронізація PostgreSQL -> MongoDB");
    Console.WriteLine("-----------------------------------");

    var reader =
        new PostgresBookCatalogReader(connectionString);

    var mongo =
        new MongoBookCatalogService(mongoConnectionString);

    var books = await reader.ReadBooksAsync();

    Console.WriteLine(
        $"Прочитано з PostgreSQL: {books.Count}");

    foreach (var book in books)
    {
        await mongo.UpsertBookAsync(book);

        Console.WriteLine(
            $"Синхронізовано: {book.Id} - {book.Title}");
    }

    long count = await mongo.CountBooksAsync();

    Console.WriteLine(
        $"Усього документів MongoDB: {count}");

    var mongoBooks = await mongo.GetAllBooksAsync();

    foreach (var book in mongoBooks.OrderBy(b => b.Id))
    {
        Console.WriteLine();
        Console.WriteLine($"Назва: {book.Title}");
        Console.WriteLine($"ISBN: {book.Isbn}");
        Console.WriteLine($"Жанр: {book.Genre}");
        Console.WriteLine($"Видавництво: {book.Publisher}");
        Console.WriteLine(
            $"Автори: {string.Join(", ", book.Authors)}");
        Console.WriteLine(
            $"Примірників: {book.TotalCopies}");
        Console.WriteLine(
            $"Доступно: {book.AvailableCopies}");

        foreach (var copy in book.Copies)
        {
            Console.WriteLine(
                $"  {copy.Barcode} | " +
                $"{copy.BranchName} | {copy.Status}");
        }
    }

    Console.WriteLine();
    Console.WriteLine("Синхронізацію завершено.");
}

// ==========================================
// ЛР №2 — REDIS
// ==========================================

async Task RunRedisDemoAsync()
{
    Console.WriteLine();
    Console.WriteLine("========== REDIS ==========");
    Console.WriteLine("Кешування каталогу бібліотеки");

    var mongo =
        new MongoBookCatalogService(mongoConnectionString);

    await using var redis =
        new RedisBookCacheService(
            redisConnectionString,
            mongo);

    const int bookId = 1;

    // Видаляємо старий кеш для демонстрації MISS.
    await redis.InvalidateBookAsync(bookId);

    Console.WriteLine();
    Console.WriteLine("1. Перше отримання книги:");

    var firstBook = await redis.GetBookAsync(bookId);

    if (firstBook is null)
    {
        Console.WriteLine(
            "Книгу не знайдено. Спочатку виконайте пункт 2.");
        return;
    }

    Console.WriteLine($"Назва: {firstBook.Title}");

    Console.WriteLine();
    Console.WriteLine("2. Повторне отримання:");

    var secondBook = await redis.GetBookAsync(bookId);

    Console.WriteLine($"Назва: {secondBook?.Title}");

    Console.WriteLine();
    Console.WriteLine("3. Час життя кешу:");

    var ttl = await redis.GetCacheTtlAsync(bookId);

    Console.WriteLine(
        $"TTL: {ttl?.TotalSeconds:F0} секунд");

    Console.WriteLine();
    Console.WriteLine("4. Лічильник переглядів:");

    long views = await redis.IncrementViewsAsync(bookId);

    Console.WriteLine(
        $"Переглядів після INCR: {views}");

    long currentViews = await redis.GetViewsAsync(bookId);

    Console.WriteLine(
        $"Поточний лічильник: {currentViews}");

    Console.WriteLine();
    Console.WriteLine("5. Інвалідація кешу:");

    await redis.InvalidateBookAsync(bookId);

    Console.WriteLine("Кеш книги видалено.");

    Console.WriteLine();
    Console.WriteLine("6. Отримання після інвалідації:");

    var restoredBook = await redis.GetBookAsync(bookId);

    Console.WriteLine(
        $"Книгу завантажено повторно: {restoredBook?.Title}");

    Console.WriteLine();
    Console.WriteLine("Перевірку Redis завершено.");
}

// ==========================================
// ЛР №2 — BENCHMARK
// ==========================================

async Task RunBenchmarkAsync()
{
    var benchmark = new Lab2Benchmark(
        connectionString,
        mongoConnectionString);

    await benchmark.RunAsync();
}

// ==========================================
// ЛР №1 — POSTGRESQL + UNIT OF WORK
// ==========================================

async Task RunLab1Async()
{
    Console.WriteLine();
    Console.WriteLine("========== ЛАБОРАТОРНА №1 ==========");

    await using var unitOfWork =
        new UnitOfWork(connectionString);

    await unitOfWork.OpenAsync();

    Console.WriteLine(
        "Підключення до PostgreSQL успішне.");

    var books =
        await unitOfWork.Books.GetAvailableBooksAsync();

    Console.WriteLine();
    Console.WriteLine("Доступні книги:");
    Console.WriteLine("-----------------------------------");

    foreach (var book in books)
    {
        Console.WriteLine(
            $"{book.BookCopyId}: " +
            $"{book.Title} | " +
            $"{book.Authors} | " +
            $"{book.BranchName} | " +
            $"{book.Barcode}");
    }

    Console.WriteLine();
    Console.WriteLine("Активні видачі:");
    Console.WriteLine("-----------------------------------");

    var loans =
        await unitOfWork.Loans.GetActiveLoansAsync();

    foreach (var loan in loans)
    {
        Console.WriteLine(
            $"{loan.LoanId}: " +
            $"{loan.ReaderName} | " +
            $"{loan.Title} | " +
            $"{loan.Barcode} | " +
            $"до {loan.DueAt:dd.MM.yyyy}");
    }

    Console.WriteLine();
    Console.WriteLine("Перевірка Unit of Work:");

    var demoBook = books.FirstOrDefault();

    if (demoBook is null)
    {
        Console.WriteLine(
            "Немає доступних книг для перевірки.");
        return;
    }

    try
    {
        await unitOfWork.BeginTransactionAsync();

        Console.WriteLine(
            $"Книга до транзакції: {demoBook.Title}");

        var dueAt = DateTime.SpecifyKind(
            DateTime.Now.AddDays(14),
            DateTimeKind.Unspecified);

        await unitOfWork.Loans.BorrowBookAsync(
            2,
            demoBook.BookCopyId,
            1,
            dueAt);

        var loansInTransaction =
            await unitOfWork.Loans.GetActiveLoansAsync();

        bool created = loansInTransaction.Any(
            x => x.BookCopyId == demoBook.BookCopyId
                 && x.ReaderId == 2);

        Console.WriteLine(
            $"Видачу створено: {created}");

        await unitOfWork.RollbackAsync();

        Console.WriteLine("Виконано ROLLBACK.");

        var booksAfterRollback =
            await unitOfWork.Books.GetAvailableBooksAsync();

        bool bookAvailable =
            booksAfterRollback.Any(
                x => x.BookCopyId == demoBook.BookCopyId);

        Console.WriteLine(
            $"Після ROLLBACK книга доступна: {bookAvailable}");
    }
    catch
    {
        try
        {
            await unitOfWork.RollbackAsync();
        }
        catch
        {
            // Транзакція могла вже завершитися.
        }

        throw;
    }
}
