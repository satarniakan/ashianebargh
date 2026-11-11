using Ashianebargh.Domain.Interfaces;

namespace Ashianebargh.Domain.Content;

/// <summary>
/// دسترسی خواندنی/نوشتنی دامنهٔ محتوا با همان DbContext و تراکنش پایه.
/// کوئری‌ها مستقیم از Application ساخته می‌شوند؛ فیلتر حذف نرم خودکار اعمال است.
/// </summary>
public interface IContentRepository
{
    IQueryable<Category> Categories { get; }
    IQueryable<Tag> Tags { get; }
    IQueryable<Product> Products { get; }
    IQueryable<ProductFile> ProductFiles { get; }
    IQueryable<Page> Pages { get; }
    IQueryable<ContactInfo> ContactInfos { get; }
    IQueryable<ContactMessage> ContactMessages { get; }

    void Add<TEntity>(TEntity entity) where TEntity : class;
    void Remove<TEntity>(TEntity entity) where TEntity : class;
}

public interface IContentUnitOfWork : IAshianebarghUnitOfWork
{
    IContentRepository Content { get; }
}
