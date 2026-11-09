using LibraryManagementApp.Models;
using Npgsql;
using NpgsqlTypes;

namespace LibraryManagementApp.Repositories
{
    public class LoanRepository : ILoanRepository
    {
        private readonly NpgsqlConnection _connection;

        public LoanRepository(NpgsqlConnection connection)
        {
            _connection = connection;
        }

        public async Task<List<ActiveLoan>> GetActiveLoansAsync()
        {
            var loans = new List<ActiveLoan>();

            const string sql = """
                SELECT
                    loan_id,
                    reader_id,
                    card_number,
                    reader_name,
                    book_id,
                    title,
                    book_copy_id,
                    barcode,
                    branch_name,
                    loaned_at,
                    due_at,
                    days_overdue,
                    issued_by
                FROM v_active_loans
                ORDER BY loaned_at;
                """;

            await using var command = new NpgsqlCommand(sql, _connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                loans.Add(new ActiveLoan
                {
                    LoanId = reader.GetInt32(
                        reader.GetOrdinal("loan_id")),

                    ReaderId = reader.GetInt32(
                        reader.GetOrdinal("reader_id")),

                    CardNumber = reader.GetString(
                        reader.GetOrdinal("card_number")),

                    ReaderName = reader.GetString(
                        reader.GetOrdinal("reader_name")),

                    BookId = reader.GetInt32(
                        reader.GetOrdinal("book_id")),

                    Title = reader.GetString(
                        reader.GetOrdinal("title")),

                    BookCopyId = reader.GetInt32(
                        reader.GetOrdinal("book_copy_id")),

                    Barcode = reader.GetString(
                        reader.GetOrdinal("barcode")),

                    BranchName = reader.GetString(
                        reader.GetOrdinal("branch_name")),

                    LoanedAt = reader.GetDateTime(
                        reader.GetOrdinal("loaned_at")),

                    DueAt = reader.GetDateTime(
                        reader.GetOrdinal("due_at")),

                    DaysOverdue = reader.GetInt32(
                        reader.GetOrdinal("days_overdue")),

                    IssuedBy = reader.GetString(
                        reader.GetOrdinal("issued_by"))
                });
            }

            return loans;
        }

        public async Task BorrowBookAsync(
            int readerId,
            int bookCopyId,
            int employeeId,
            DateTime dueAt)
        {
            const string sql = """
                CALL borrow_book(
                    @p_reader_id,
                    @p_book_copy_id,
                    @p_employee_id,
                    @p_due_at
                );
                """;

            await using var command = new NpgsqlCommand(sql, _connection);

            command.Parameters.AddWithValue(
                "p_reader_id",
                readerId);

            command.Parameters.AddWithValue(
                "p_book_copy_id",
                bookCopyId);

            command.Parameters.AddWithValue(
                "p_employee_id",
                employeeId);

            command.Parameters.Add(
                "p_due_at",
                NpgsqlDbType.Timestamp).Value = dueAt;

            await command.ExecuteNonQueryAsync();
        }

        public async Task ReturnBookAsync(
            int loanId,
            int employeeId)
        {
            const string sql = """
                CALL return_book(
                    @p_loan_id,
                    @p_employee_id
                );
                """;

            await using var command = new NpgsqlCommand(sql, _connection);

            command.Parameters.AddWithValue(
                "p_loan_id",
                loanId);

            command.Parameters.AddWithValue(
                "p_employee_id",
                employeeId);

            await command.ExecuteNonQueryAsync();
        }
    }
}