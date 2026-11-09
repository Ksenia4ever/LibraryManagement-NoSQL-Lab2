using System;
using System.Collections.Generic;
using System.Text;

namespace LibraryManagementApp.Models
{
    public class ActiveLoan
    {
        public int LoanId { get; set; }

        public int ReaderId { get; set; }
        public string CardNumber { get; set; } = string.Empty;
        public string ReaderName { get; set; } = string.Empty;

        public int BookId { get; set; }
        public string Title { get; set; } = string.Empty;

        public int BookCopyId { get; set; }
        public string Barcode { get; set; } = string.Empty;

        public string BranchName { get; set; } = string.Empty;

        public DateTime LoanedAt { get; set; }
        public DateTime DueAt { get; set; }

        public int DaysOverdue { get; set; }

        public string IssuedBy { get; set; } = string.Empty;
    }
}