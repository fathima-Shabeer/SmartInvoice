using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartInvoice.Data;
using SmartInvoice.Models;
using SmartInvoice.ViewModels;

namespace SmartInvoice.Controllers;

public class HomeController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        // Small portfolio dataset: aggregate in memory (keeps the queries simple).
        var invoices = await db.Invoices
            .Include(i => i.Vendor).Include(i => i.Category).Include(i => i.Flags)
            .AsNoTracking().ToListAsync();

        var active = invoices.Where(i => i.Status != InvoiceStatus.Rejected).ToList();
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        var thisMonth = active.Where(i => i.Date >= monthStart).Sum(i => i.Total);
        var lastMonth = active.Where(i => i.Date >= monthStart.AddMonths(-1) && i.Date < monthStart).Sum(i => i.Total);

        var months = Enumerable.Range(0, 6).Select(k => monthStart.AddMonths(-5 + k)).ToList();
        var pending = invoices.Where(i => i.Status is InvoiceStatus.Submitted or InvoiceStatus.NeedsReview).ToList();

        var byCategory = active
            .GroupBy(i => i.Category?.Name ?? "Other")
            .Select(g => new { Name = g.Key, Color = g.First().Category?.Color ?? "#8A9099", Total = g.Sum(i => i.Total) })
            .OrderByDescending(x => x.Total)
            .ToList();

        var vm = new DashboardViewModel
        {
            InvoiceCount = invoices.Count,
            ThisMonthSpend = thisMonth,
            MonthChangePct = lastMonth > 0 ? Math.Round((thisMonth - lastMonth) / lastMonth * 100, 1) : 0,
            PendingAmount = pending.Sum(i => i.Total),
            PendingCount = pending.Count,
            FlaggedCount = invoices.Count(i => i.Flags.Count > 0 && i.Status == InvoiceStatus.NeedsReview),
            MonthLabels = months.Select(m => m.ToString("MMM")).ToList(),
            MonthTotals = months.Select(m => active.Where(i => i.Date.Year == m.Year && i.Date.Month == m.Month).Sum(i => i.Total)).ToList(),
            CategoryLabels = byCategory.Select(c => c.Name).ToList(),
            CategoryTotals = byCategory.Select(c => c.Total).ToList(),
            CategoryColors = byCategory.Select(c => c.Color).ToList(),
            Flagged = invoices.Where(i => i.Status == InvoiceStatus.NeedsReview).OrderByDescending(i => i.Date).Take(5).ToList(),
            Recent = invoices.OrderByDescending(i => i.Date).ThenByDescending(i => i.Id).Take(6).ToList(),
        };

        return View(vm);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}
