using LibraryManagementApp.Models;
using Npgsql;

namespace LibraryManagementApp.Repositories
{
    public class BookRepository : IBookRepository
    {
        private readonly NpgsqlConnection _connection;

        public BookRepository(NpgsqlConnection connection)
        {
            _connection = connection;
        }

        public async Task<List<AvailableBook>> GetAvailableBooksAsync()
        {
            var books = new List<AvailableBook>();

            const string sql = """
                SELECT
                    book_copy_id,
                    book_id,
                    title,
                    isbn,
                    authors,
                    genre,
                    publisher,
                    branch_id,
                    branch_name,
                    barcode
                FROM v_available_books
                ORDER BY title, branch_name;
                """;

            await using var command = new NpgsqlCommand(sql, _connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                books.Add(new AvailableBook
                {
                    BookCopyId = reader.GetInt32(
                        reader.GetOrdinal("book_copy_id")),

                    BookId = reader.GetInt32(
                        reader.GetOrdinal("book_id")),

                    Title = reader.GetString(
                        reader.GetOrdinal("title")),

                    Isbn = reader.GetString(
                        reader.GetOrdinal("isbn")),

                    Authors = reader.IsDBNull(
                        reader.GetOrdinal("authors"))
                            ? null
                            : reader.GetString(
                                reader.GetOrdinal("authors")),

                    Genre = reader.GetString(
                        reader.GetOrdinal("genre")),

                    Publisher = reader.GetString(
                        reader.GetOrdinal("publisher")),

                    BranchId = reader.GetInt32(
                        reader.GetOrdinal("branch_id")),

                    BranchName = reader.GetString(
                        reader.GetOrdinal("branch_name")),

                    Barcode = reader.GetString(
                        reader.GetOrdinal("barcode"))
                });
            }

            return books;
        }
    }
}