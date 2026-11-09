using LibraryManagementApp.Repositories;

namespace LibraryManagementApp.UnitOfWork
{
    public interface IUnitOfWork : IAsyncDisposable
    {
        IBookRepository Books { get; }
        ILoanRepository Loans { get; }

        Task OpenAsync();

        Task BeginTransactionAsync();
        Task CommitAsync();
        Task RollbackAsync();
    }
}