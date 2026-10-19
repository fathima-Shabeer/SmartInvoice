using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SmartInvoice.Models;

namespace SmartInvoice.ViewModels;

public class DashboardViewModel
{
    public int InvoiceCount { get; set; }
    public decimal ThisMonthSpend { get; set; }
    public decimal MonthChangePct { get; set; }
    public decimal PendingAmount { get; set; }
    public int PendingCount { get; set; }
    public int FlaggedCount { get; set; }

    public List<string> MonthLabels { get; set; } = new();
    public List<decimal> MonthTotals { get; set; } = new();
    public List<string> CategoryLabels { get; set; } = new();
    public List<decimal> CategoryTotals { get; set; } = new();
    public List<string> CategoryColors { get; set; } = new();

    public List<Invoice> Flagged { get; set; } = new();
    public List<Invoice> Recent { get; set; } = new();
}

public class InvoiceListViewModel
{
    public List<Invoice> Items { get; set; } = new();
    public string? Status { get; set; }
    public string? Query { get; set; }
    public int All { get; set; }
    public Dictionary<InvoiceStatus, int> Counts { get; set; } = new();
}

public class InvoiceFormViewModel
{
    [Required, StringLength(40), Display(Name = "Invoice number")]
    public string InvoiceNumber { get; set; } = "";

    [Required, StringLength(120), Display(Name = "Vendor")]
    public string VendorName { get; set; } = "";

    [DataType(DataType.Date), DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true), Display(Name = "Invoice date")]
    public DateTime Date { get; set; } = DateTime.Today;

    [Required, Range(0.01, 10000000), Display(Name = "Subtotal (AED)")]
    public decimal? Subtotal { get; set; }

    [Range(0, 10000000), Display(Name = "VAT (AED)")]
    public decimal? VatAmount { get; set; }

    [Display(Name = "Category")]
    public int? CategoryId { get; set; }

    [StringLength(300)]
    public string? Description { get; set; }

    [Required, StringLength(80), Display(Name = "Submitted by")]
    public string SubmittedBy { get; set; } = "";

    [Display(Name = "Invoice file")]
    public IFormFile? File { get; set; }

    [BindNever]
    public List<Category> Categories { get; set; } = new();
}
