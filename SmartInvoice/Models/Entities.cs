using System.ComponentModel.DataAnnotations.Schema;

namespace SmartInvoice.Models;

public enum InvoiceStatus { NeedsReview, Submitted, Approved, Rejected }

public enum AnomalyType { Duplicate, Outlier, NewVendor, VatMismatch }

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Comma-separated keywords used by the smart categorizer.</summary>
    public string Keywords { get; set; } = "";
    public string Color { get; set; } = "#3556A3";
}

public class Vendor
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<Invoice> Invoices { get; set; } = new();
}

public class Invoice
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = "";

    public int VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public DateTime Date { get; set; } = DateTime.Today;
    public decimal Subtotal { get; set; }
    public decimal VatAmount { get; set; }
    public string Currency { get; set; } = "AED";
    public string? Description { get; set; }
    public string? FilePath { get; set; }
    public string SubmittedBy { get; set; } = "";
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Submitted;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<AnomalyFlag> Flags { get; set; } = new();

    [NotMapped]
    public decimal Total => Subtotal + VatAmount;
}

public class AnomalyFlag
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public AnomalyType Type { get; set; }
    public string Message { get; set; } = "";
}
