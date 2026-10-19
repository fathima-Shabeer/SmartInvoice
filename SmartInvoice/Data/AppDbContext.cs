using Microsoft.EntityFrameworkCore;
using SmartInvoice.Models;

namespace SmartInvoice.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<AnomalyFlag> AnomalyFlags => Set<AnomalyFlag>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Invoice>().Property(i => i.Subtotal).HasPrecision(18, 2);
        b.Entity<Invoice>().Property(i => i.VatAmount).HasPrecision(18, 2);
        b.Entity<Invoice>().HasMany(i => i.Flags).WithOne(f => f.Invoice).HasForeignKey(f => f.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Vendor>().HasIndex(v => v.Name);
    }
}
