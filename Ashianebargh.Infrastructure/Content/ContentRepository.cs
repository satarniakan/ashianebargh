using Ashianebargh.Domain.Content;
using Ashianebargh.Infrastructure.Data;

namespace Ashianebargh.Infrastructure.Content;

public sealed class ContentRepository(AshianebarghDbContext context) : IContentRepository
{
    public IQueryable<Category> Categories => context.Set<Category>();
    public IQueryable<Tag> Tags => context.Set<Tag>();
    public IQueryable<Product> Products => context.Set<Product>();
    public IQueryable<Page> Pages => context.Set<Page>();
    public IQueryable<ContactInfo> ContactInfos => context.Set<ContactInfo>();
    public IQueryable<ContactMessage> ContactMessages => context.Set<ContactMessage>();

    public void Add<TEntity>(TEntity entity) where TEntity : class => context.Add(entity);

    public void Remove<TEntity>(TEntity entity) where TEntity : class => context.Remove(entity);
}
