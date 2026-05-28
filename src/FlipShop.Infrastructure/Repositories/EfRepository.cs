using FlipShop.Application.Interfaces;
using FlipShop.Domain.Common;
using FlipShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace FlipShop.Infrastructure.Repositories;

public sealed class EfRepository<T>(AppDbContext dbContext) : IRepository<T> where T : BaseEntity
{
    public IQueryable<T> Query() => dbContext.Set<T>().AsQueryable();
    public Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default) => dbContext.Set<T>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    public async Task AddAsync(T entity, CancellationToken cancellationToken = default) => await dbContext.Set<T>().AddAsync(entity, cancellationToken);
    public void Update(T entity) => dbContext.Set<T>().Update(entity);
    public void Remove(T entity) => entity.IsActive = false;
}

public sealed class UnitOfWork(AppDbContext dbContext, IServiceProvider serviceProvider) : IUnitOfWork
{
    public IRepository<T> Repository<T>() where T : BaseEntity => serviceProvider.GetRequiredService<IRepository<T>>();
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);

    public async Task<IDisposable> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        return transaction;
    }
}
