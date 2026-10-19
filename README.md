# SmartInvoice

A smart invoice and expense management web app built with ASP.NET Core 8, Entity Framework Core, and SQL Server. It checks every invoice for problems before a manager approves it.




## The problem

Finance teams receive invoices from many vendors. Duplicates, wrong VAT, and unusually large amounts are easy to miss when approvals are done by hand. SmartInvoice flags these issues automatically so the reviewer can focus on the invoices that need attention.

## Features

- **Dashboard:** monthly spend, change versus last month, amount awaiting approval, and a count of flagged invoices
- **Charts:** six-month spend trend and spend by category (Chart.js)
- **Smart category suggestion:** reads the vendor name and description and suggests a category while you type
- **Anomaly detection**
  - possible duplicate invoices
  - amounts far above a vendor's usual range
  - first invoice from a new vendor
  - VAT that does not match the 5% UAE rate
- **Approval workflow:** needs review, awaiting approval, approved, rejected
- **Invoice management:** create, search, filter by status, attach a PDF or image, delete
- **CSV export** that opens correctly in Excel
- **Responsive layout** for desktop and mobile


## Tech stack

| Area | Technology |
|---|---|
| Backend | ASP.NET Core 8 MVC, C# 12 |
| Data | Entity Framework Core 8, SQL Server |
| Frontend | Razor views, Bootstrap 5, Chart.js |
| Design | Custom CSS, IBM Plex Sans, Newsreader |

## Architecture

```
Controllers   HTTP actions: dashboard, invoice CRUD, approval, export
Services      SmartService: category suggestion and anomaly detection
Data          AppDbContext, seed data
Models        Invoice, Vendor, Category, AnomalyFlag
ViewModels    Shapes passed to the views
Views         Razor pages and shared layout
```

The smart logic sits behind an `ISmartService` interface, so the rule-based checks can be replaced with ML.NET or an AI service without changing the controllers.

## How the checks work

| Check | Rule |
|---|---|
| Duplicate | Same vendor with the same invoice number, or the same date and total |
| Unusual amount | Total is above the vendor's average plus two standard deviations, or 1.75 times the average, based on at least three earlier invoices |
| New vendor | No earlier invoices from this vendor |
| VAT mismatch | VAT differs from 5% of the subtotal by more than 0.50 AED |

## Run locally

Requirements: .NET 8 SDK and SQL Server (Express, LocalDB, or full).

```bash
git clone https://github.com/<your-username>/SmartInvoice.git
cd SmartInvoice
dotnet restore
dotnet run
```

By default the app connects to LocalDB. To use your own server, set the connection string in `appsettings.json`:

```json
"ConnectionStrings": {
  "Default": "Server=localhost;Database=SmartInvoiceDb;Trusted_Connection=True;TrustServerCertificate=True"
}
```

The database is created and filled with demo data on first run.

## Roadmap

- [ ] Login and roles (Employee, Manager, Finance, Admin)
- [ ] OCR extraction from uploaded invoices
- [ ] ML.NET model for category prediction
- [ ] Unit tests for the anomaly rules
- [ ] Docker and CI/CD with GitHub Actions





## Author

**Your name** ·[Fathima-Shabeer]

