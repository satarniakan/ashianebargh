using Microsoft.EntityFrameworkCore;
using Ashianebargh.Domain.Content;
using Ashianebargh.Infrastructure.Data;

namespace Ashianebargh.Infrastructure.Content;

/// <summary>جدول‌های دامنهٔ محتوا؛ بدون ویرایش پایه و قابل کشف در design-time.</summary>
public sealed class ContentModelModule : IAshianebarghModule
{
    public string Name => "Content";

    public void ConfigureModel(ModelBuilder builder)
    {
        builder.Entity<Category>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(150).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.HasIndex(x => x.Slug).IsUnique().HasFilter("[IsDeleted] = 0");
            e.HasIndex(x => new { x.IsActive, x.SortOrder });
        });

        builder.Entity<Tag>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        builder.Entity<Product>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(200).IsRequired();
            e.Property(x => x.Code).HasMaxLength(50);
            e.Property(x => x.ShortDescription).HasMaxLength(500);
            e.Property(x => x.Price).HasPrecision(18, 0);
            e.Property(x => x.MainImageStorageId).HasMaxLength(100);
            e.HasIndex(x => x.Slug).IsUnique().HasFilter("[IsDeleted] = 0");
            e.HasIndex(x => new { x.IsActive, x.SortOrder });
            e.HasIndex(x => x.IsFeatured);
            e.HasOne(x => x.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            // رابطهٔ چندبه‌چند با جدول واسط خودکار EF
            e.HasMany(x => x.Tags).WithMany(t => t.Products).UsingEntity("ProductTags");
        });

        builder.Entity<Page>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique().HasFilter("[IsDeleted] = 0");
            e.HasIndex(x => new { x.IsPublished, x.ShowInMenu, x.SortOrder });
        });

        builder.Entity<ContactInfo>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.Phone).HasMaxLength(100);
            e.Property(x => x.Mobile).HasMaxLength(100);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.WorkingHours).HasMaxLength(200);
            e.Property(x => x.MapUrl).HasMaxLength(500);
            e.Property(x => x.Instagram).HasMaxLength(300);
            e.Property(x => x.Telegram).HasMaxLength(300);
            e.Property(x => x.WhatsApp).HasMaxLength(300);
        });

        builder.Entity<ContactMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(30).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Subject).HasMaxLength(200);
            e.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            e.HasIndex(x => new { x.IsRead, x.CreatedAt });
        });
    }
}
