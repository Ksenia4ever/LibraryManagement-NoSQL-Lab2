using LibraryManagementApp.Repositories;
using Npgsql;
using System.Data;

namespace LibraryManagementApp.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly NpgsqlConnection _connection;
        private NpgsqlTransaction? _transaction;

        public IBookRepository Books { get; }
        public ILoanRepository Loans { get; }

        public UnitOfWork(string connectionString)
        {
            _connection = new NpgsqlConnection(connectionString);

            Books = new BookRepository(_connection);
            Loans = new LoanRepository(_connection);
        }

        public async Task OpenAsync()
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }
        }

        public async Task BeginTransactionAsync()
        {
            if (_transaction != null)
            {
                throw new InvalidOperationException(
                    "Транзакція вже розпочата.");
            }

            _transaction =
                await _connection.BeginTransactionAsync();
        }

        public async Task CommitAsync()
        {
            if (_transaction == null)
            {
                throw new InvalidOperationException(
                    "Немає активної транзакції.");
            }

            await _transaction.CommitAsync();
            await _transaction.DisposeAsync();

            _transaction = null;
        }

        public async Task RollbackAsync()
        {
            if (_transaction == null)
            {
                throw new InvalidOperationException(
                    "Немає активної транзакції.");
            }

            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();

            _transaction = null;
        }

        public async ValueTask DisposeAsync()
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
            }

            await _connection.DisposeAsync();
        }
    }
}