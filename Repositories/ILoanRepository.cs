using LibraryManagementApp.Models;

namespace LibraryManagementApp.Repositories
{
    public interface ILoanRepository
    {
        Task<List<ActiveLoan>> GetActiveLoansAsync();

        Task BorrowBookAsync(
            int readerId,
            int bookCopyId,
            int employeeId,
            DateTime dueAt);

        Task ReturnBookAsync(
            int loanId,
            int employeeId);
    }
}