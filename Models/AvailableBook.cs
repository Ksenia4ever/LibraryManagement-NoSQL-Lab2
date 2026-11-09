using System;
using System.Collections.Generic;
using System.Text;

namespace LibraryManagementApp.Models
{
    public class AvailableBook
    {
        public int BookCopyId { get; set; }
        public int BookId { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Isbn { get; set; } = string.Empty;
        public string? Authors { get; set; }
        public string Genre { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;

        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;

        public string Barcode { get; set; } = string.Empty;
    }
}
