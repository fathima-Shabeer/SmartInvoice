using SmartInvoice.Models;

namespace SmartInvoice.Services;

public interface ISmartService
{
    Category SuggestCategory(string vendor, string? description, IReadOnlyList<Category> categories);
    List<AnomalyFlag> Detect(Invoice invoice, IReadOnlyList<Invoice> previousFromSameVendor);
}

/// <summary>
/// Rule-based "smart" features: keyword categorization and anomaly detection.
/// Swap the internals for ML.NET / Azure AI later without touching controllers.
/// </summary>
public class SmartService : ISmartService
{
    private const decimal VatRate = 0.05m; // UAE VAT

    public Category SuggestCategory(string vendor, string? description, IReadOnlyList<Category> categories)
    {
        var text = $"{vendor} {description}".ToLowerInvariant();

        var best = categories
            .Select(c => new
            {
                Category = c,
                Score = c.Keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                  .Count(k => text.Contains(k.ToLowerInvariant()))
            })
            .OrderByDescending(x => x.Score)
            .First();

        if (best.Score > 0) return best.Category;

        return categories.FirstOrDefault(c => c.Name == "Other") ?? categories.First();
    }

    public List<AnomalyFlag> Detect(Invoice invoice, IReadOnlyList<Invoice> previousFromSameVendor)
    {
        var flags = new List<AnomalyFlag>();
        var others = previousFromSameVendor.Where(o => o.Id != invoice.Id).ToList();

        // 1. Duplicates: same number, or same date and amount for the same vendor
        var duplicate = others.FirstOrDefault(o =>
            o.InvoiceNumber.Equals(invoice.InvoiceNumber, StringComparison.OrdinalIgnoreCase) ||
            (o.Date.Date == invoice.Date.Date && o.Total == invoice.Total));
        if (duplicate is not null)
        {
            flags.Add(new AnomalyFlag
            {
                Type = AnomalyType.Duplicate,
                Message = $"Looks like a duplicate of {duplicate.InvoiceNumber} from {duplicate.Date:dd MMM yyyy}."
            });
        }

        // 2. First invoice from this vendor
        if (others.Count == 0)
        {
            flags.Add(new AnomalyFlag
            {
                Type = AnomalyType.NewVendor,
                Message = "First invoice from this vendor. Confirm the vendor is approved."
            });
        }
        // 3. Amount far above this vendor's usual range
        else if (others.Count >= 3)
        {
            var totals = others.Select(o => (double)o.Total).ToList();
            var mean = totals.Average();
            var std = Math.Sqrt(totals.Sum(t => Math.Pow(t - mean, 2)) / totals.Count);
            var threshold = Math.Max(mean + 2 * std, mean * 1.75);

            if ((double)invoice.Total > threshold)
            {
                flags.Add(new AnomalyFlag
                {
                    Type = AnomalyType.Outlier,
                    Message = $"Amount is well above this vendor's usual {mean:N0} AED average."
                });
            }
        }

        // 4. VAT does not match the 5% UAE rate
        var expectedVat = Math.Round(invoice.Subtotal * VatRate, 2);
        if (invoice.VatAmount > 0 && Math.Abs(invoice.VatAmount - expectedVat) > 0.5m)
        {
            flags.Add(new AnomalyFlag
            {
                Type = AnomalyType.VatMismatch,
                Message = $"VAT is {invoice.VatAmount:N2} but 5% of the subtotal is {expectedVat:N2}."
            });
        }

        return flags;
    }
}
