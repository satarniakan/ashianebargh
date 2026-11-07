using Ashianebargh.Domain.Content;
using Ashianebargh.Infrastructure.Data;
using Ashianebargh.Infrastructure.Repositories;

namespace Ashianebargh.Infrastructure.Content;

/// <summary>همان واحد کار پایه با ریپازیتوری محتوا روی همان DbContext و تراکنش.</summary>
public sealed class ContentUnitOfWork(AshianebarghDbContext context, IServiceProvider services)
    : AshianebarghUnitOfWork(context, services), IContentUnitOfWork
{
    public IContentRepository Content { get; } = new ContentRepository(context);
}
