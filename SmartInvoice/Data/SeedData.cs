using SmartInvoice.Models;
using SmartInvoice.Services;

namespace SmartInvoice.Data;

public static class SeedData
{
    public static void Initialize(AppDbContext db, ISmartService smart)
    {
        if (db.Categories.Any()) return;

        var categories = new List<Category>
        {
            new() { Name = "Software & Cloud", Color = "#3556A3", Keywords = "azure,aws,amazon web,google cloud,microsoft,github,adobe,software,subscription,hosting,license,saas" },
            new() { Name = "Travel", Color = "#0E7C66", Keywords = "emirates,flydubai,etihad,airline,hotel,booking,uber,careem,taxi,flight,airbnb" },
            new() { Name = "Meals & Entertainment", Color = "#B7791F", Keywords = "restaurant,cafe,starbucks,talabat,deliveroo,lunch,dinner,catering,coffee" },
            new() { Name = "Office Supplies", Color = "#7A5C8E", Keywords = "staples,office,stationery,paper,printer,ink,desk,furniture" },
            new() { Name = "Utilities & Telecom", Color = "#4F8A9E", Keywords = "dewa,etisalat,internet,electricity,water,telecom,mobile" },
            new() { Name = "Marketing", Color = "#C2362B", Keywords = "google ads,meta,facebook,linkedin,campaign,advert,promotion,design,print" },
            new() { Name = "Professional Services", Color = "#16213A", Keywords = "consult,legal,audit,accounting,law,advisory,recruit" },
            new() { Name = "Other", Color = "#8A9099", Keywords = "" },
        };
        db.Categories.AddRange(categories);

        var vendorSpecs = new (string Name, string Prefix, string Category, decimal Base, string Desc)[]
        {
            ("Amazon Web Services", "AWS", "Software & Cloud", 2000, "Monthly cloud hosting"),
            ("Microsoft Azure", "AZR", "Software & Cloud", 1500, "Azure subscription"),
            ("Emirates Airline", "EK", "Travel", 2600, "Business flight"),
            ("Careem Business", "CRM", "Travel", 240, "Client transport"),
            ("Starbucks Dubai Mall", "SBX", "Meals & Entertainment", 180, "Team coffee"),
            ("Staples Middle East", "STP", "Office Supplies", 520, "Stationery and printer ink"),
            ("Etisalat Business", "ETS", "Utilities & Telecom", 900, "Office internet and mobile"),
            ("Google Ads", "GAD", "Marketing", 3200, "Search campaign"),
        };

        var vendors = vendorSpecs.Select(v => new Vendor { Name = v.Name }).ToList();
        vendors.Add(new Vendor { Name = "Al Noor Consultancy FZ LLC" });
        db.Vendors.AddRange(vendors);
        db.SaveChanges();

        var rng = new Random(42);
        var invoices = new List<Invoice>();
        var today = DateTime.Today;
        var n = 1000;

        // Regular history spread over the last ~6 months
        for (var m = 5; m >= 0; m--)
        {
            for (var v = 0; v < vendorSpecs.Length; v++)
            {
                if (rng.NextDouble() < 0.25) continue;
                var spec = vendorSpecs[v];
                var date = new DateTime(today.Year, today.Month, 1).AddMonths(-m).AddDays(rng.Next(0, 26));
                if (date > today) date = today.AddDays(-rng.Next(1, 5));
                var subtotal = Math.Round(spec.Base * (decimal)(0.85 + rng.NextDouble() * 0.3), 2);
                invoices.Add(NewInvoice(spec.Prefix, ++n, vendors[v], categories.First(c => c.Name == spec.Category), date, subtotal, spec.Desc));
            }
        }

        // Planted anomalies for the demo
        invoices.Add(NewInvoice("AWS", ++n, vendors[0], categories[0], today.AddDays(-3), 9400m, "Monthly cloud hosting"));

        var dupSource = invoices.First(i => i.VendorId == vendors[2].Id);
        var dup = NewInvoice("EK", 0, vendors[2], categories[1], dupSource.Date.AddDays(2), dupSource.Subtotal, "Business flight");
        dup.InvoiceNumber = dupSource.InvoiceNumber;
        invoices.Add(dup);

        var vatBad = NewInvoice("CRM", ++n, vendors[3], categories[1], today.AddDays(-6), 1800m, "Client transport");
        vatBad.VatAmount = 360m;
        invoices.Add(vatBad);

        invoices.Add(NewInvoice("ANC", ++n, vendors[^1], categories[6], today.AddDays(-2), 12500m, "Legal advisory retainer"));

        // Run the smart checks in date order so each invoice sees only its history
        var seen = new List<Invoice>();
        foreach (var inv in invoices.OrderBy(i => i.Date).ThenBy(i => i.InvoiceNumber))
        {
            inv.Flags = smart.Detect(inv, seen.Where(s => s.VendorId == inv.VendorId).ToList());
            inv.Status = inv.Flags.Count > 0 ? InvoiceStatus.NeedsReview
                       : inv.Date < today.AddDays(-20) ? InvoiceStatus.Approved
                       : InvoiceStatus.Submitted;
            seen.Add(inv);
        }

        db.Invoices.AddRange(invoices);
        db.SaveChanges();
    }

    private static Invoice NewInvoice(string prefix, int number, Vendor vendor, Category category, DateTime date, decimal subtotal, string desc) => new()
    {
        InvoiceNumber = $"{prefix}-{number}",
        VendorId = vendor.Id,
        CategoryId = category.Id,
        Date = date,
        Subtotal = subtotal,
        VatAmount = Math.Round(subtotal * 0.05m, 2),
        Description = desc,
        SubmittedBy = "Sara Khan",
    };
}
