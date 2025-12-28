using FluentResults;
using Pepegov.UnitOfWork.EntityFramework.Repository;
using Pepegov.UnitOfWork.Repository;

namespace User.Infrastructure;

public interface IRepositoryProvider
{
    IRepositoryEntityFramework<TEntity> GetRepository<TEntity>()
        where TEntity : class;

    Result SaveChanges();
}
