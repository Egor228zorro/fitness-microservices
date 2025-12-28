using FluentResults;
using Pepegov.UnitOfWork.EntityFramework.Repository;

namespace PublicWorkout.Infrastructure;

public interface IRepositoryProvider
{
    IRepositoryEntityFramework<TEntity> GetRepository<TEntity>()
        where TEntity : class;

    Result SaveChanges();
}
