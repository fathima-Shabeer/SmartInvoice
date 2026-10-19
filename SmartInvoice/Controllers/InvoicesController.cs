using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartInvoice.Data;
using SmartInvoice.Models;
using SmartInvoice.Services;
using SmartInvoice.ViewModels;

namespace SmartInvoice.Controllers;

public class InvoicesController(AppDbContext db, ISmartService smart, IWebHostEnvironment env) : Controller
{
    private static readonly string[] AllowedExtensions = [".pdf", ".png", ".jpg", ".jpeg"];
    private const long MaxFileBytes = 5 * 1024 * 1024;

    public async Task<IActionResult> Index(string? status, string? q)
    {
        var query = db.Invoices
            .Include(i => i.Vendor).Include(i => i.Category).Include(i => i.Flags)
            .AsNoTracking().AsQueryable();

        if (Enum.TryParse<InvoiceStatus>(status, out var parsed))
            query = query.Where(i => i.Status == parsed);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(i => i.Vendor!.Name.Contains(q) || i.InvoiceNumber.Contains(q));

        var counts = await db.Invoices.GroupBy(i => i.Status)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();

        var vm = new InvoiceListViewModel
        {
            Items = await query.OrderByDescending(i => i.Date).ThenByDescending(i => i.Id).ToListAsync(),
            Status = status,
            Query = q,
            All = counts.Sum(c => c.Count),
            Counts = counts.ToDictionary(c => c.Key, c => c.Count),
        };
        return View(vm);
    }

    public async Task<IActionResult> Details(int id)
    {
        var invoice = await db.Invoices
            .Include(i => i.Vendor).Include(i => i.Category).Include(i => i.Flags)
            .AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
        return invoice is null ? NotFound() : View(invoice);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
        => View(new InvoiceFormViewModel { Categories = await db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync() });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InvoiceFormViewModel vm)
    {
        if (vm.File is { Length: > 0 })
        {
            var ext = Path.GetExtension(vm.File.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                ModelState.AddModelError(nameof(vm.File), "Upload a PDF, PNG or JPG file.");
            else if (vm.File.Length > MaxFileBytes)
                ModelState.AddModelError(nameof(vm.File), "The file is larger than 5 MB.");
        }

        var categories = await db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        if (!ModelState.IsValid)
        {
            vm.Categories = categories;
            return View(vm);
        }

        var vendorName = vm.VendorName.Trim();
        var vendor = await db.Vendors.FirstOrDefaultAsync(v => v.Name.ToLower() == vendorName.ToLower());
        if (vendor is null)
        {
            vendor = new Vendor { Name = vendorName };
            db.Vendors.Add(vendor);
            await db.SaveChangesAsync();
        }

        var category = categories.FirstOrDefault(c => c.Id == vm.CategoryId)
                       ?? smart.SuggestCategory(vendorName, vm.Description, categories);

        var subtotal = vm.Subtotal!.Value;
        var invoice = new Invoice
        {
            InvoiceNumber = vm.InvoiceNumber.Trim(),
            VendorId = vendor.Id,
            CategoryId = category.Id,
            Date = vm.Date,
            Subtotal = subtotal,
            VatAmount = vm.VatAmount ?? Math.Round(subtotal * 0.05m, 2),
            Description = vm.Description?.Trim(),
            SubmittedBy = vm.SubmittedBy.Trim(),
        };

        if (vm.File is { Length: > 0 })
            invoice.FilePath = await SaveFileAsync(vm.File);

        var history = await db.Invoices.AsNoTracking().Where(i => i.VendorId == vendor.Id).ToListAsync();
        invoice.Flags = smart.Detect(invoice, history);
        invoice.Status = invoice.Flags.Count > 0 ? InvoiceStatus.NeedsReview : InvoiceStatus.Submitted;

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        TempData["Toast"] = invoice.Flags.Count > 0
            ? $"Invoice {invoice.InvoiceNumber} saved. {invoice.Flags.Count} item(s) need your review."
            : $"Invoice {invoice.InvoiceNumber} saved and sent for approval.";
        return RedirectToAction(nameof(Details), new { id = invoice.Id });
    }

    /// <summary>Live category suggestion used by the Create form.</summary>
    [HttpGet]
    public async Task<IActionResult> Suggest(string? vendor, string? description)
    {
        if (string.IsNullOrWhiteSpace(vendor) && string.IsNullOrWhiteSpace(description))
            return Json(new { found = false });

        var categories = await db.Categories.AsNoTracking().ToListAsync();
        var match = smart.SuggestCategory(vendor ?? "", description, categories);
        return Json(new { found = match.Name != "Other", id = match.Id, name = match.Name, color = match.Color });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Approve(int id) => Decide(id, InvoiceStatus.Approved, "approved");

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Reject(int id) => Decide(id, InvoiceStatus.Rejected, "rejected");

    private async Task<IActionResult> Decide(int id, InvoiceStatus newStatus, string verb)
    {
        var invoice = await db.Invoices.FindAsync(id);
        if (invoice is null) return NotFound();

        if (invoice.Status is InvoiceStatus.Submitted or InvoiceStatus.NeedsReview)
        {
            invoice.Status = newStatus;
            await db.SaveChangesAsync();
            TempData["Toast"] = $"Invoice {invoice.InvoiceNumber} {verb}.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var invoice = await db.Invoices.FindAsync(id);
        if (invoice is null) return NotFound();

        if (!string.IsNullOrEmpty(invoice.FilePath))
        {
            var physical = Path.Combine(env.WebRootPath, invoice.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(physical)) System.IO.File.Delete(physical);
        }

        db.Invoices.Remove(invoice);
        await db.SaveChangesAsync();
        TempData["Toast"] = $"Invoice {invoice.InvoiceNumber} deleted.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Export(string? status)
    {
        var query = db.Invoices.Include(i => i.Vendor).Include(i => i.Category).AsNoTracking().AsQueryable();
        if (Enum.TryParse<InvoiceStatus>(status, out var parsed))
            query = query.Where(i => i.Status == parsed);

        var rows = await query.OrderBy(i => i.Date).ToListAsync();

        var sb = new StringBuilder("Invoice,Vendor,Category,Date,Subtotal,VAT,Total,Currency,Status,Submitted by\n");
        foreach (var i in rows)
        {
            sb.AppendJoin(',',
                Csv(i.InvoiceNumber), Csv(i.Vendor?.Name), Csv(i.Category?.Name), i.Date.ToString("yyyy-MM-dd"),
                i.Subtotal.ToString("0.00"), i.VatAmount.ToString("0.00"), i.Total.ToString("0.00"),
                i.Currency, i.Status, Csv(i.SubmittedBy)).AppendLine();
        }

        var bytes = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"invoices-{DateTime.Today:yyyyMMdd}.csv");
    }

    private static string Csv(string? value) => $"\"{(value ?? "").Replace("\"", "\"\"")}\"";

    private async Task<string> SaveFileAsync(IFormFile file)
    {
        var folder = Path.Combine(env.WebRootPath, "uploads");
        Directory.CreateDirectory(folder);
        var name = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        await using var stream = System.IO.File.Create(Path.Combine(folder, name));
        await file.CopyToAsync(stream);
        return $"/uploads/{name}";
    }
}
