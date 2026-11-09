using LibraryManagementApp.Models;

namespace LibraryManagementApp.Repositories
{
    public interface IBookRepository
    {
        Task<List<AvailableBook>> GetAvailableBooksAsync();
    }
}