using FluentResults;
using Pepegov.UnitOfWork;
using Pepegov.UnitOfWork.EntityFramework;
using Pepegov.UnitOfWork.EntityFramework.Repository;
using Pepegov.UnitOfWork.Repository;

namespace User.Infrastructure;

public class RepositoryProvider : IRepositoryProvider
{
    private readonly IUnitOfWorkEntityFrameworkInstance unitOfWorkInstance;

    public RepositoryProvider(IUnitOfWorkManager unitOfWorkManager)
    {
        this.unitOfWorkInstance =
            unitOfWorkManager.GetInstance<IUnitOfWorkEntityFrameworkInstance>();
        this.unitOfWorkInstance.SetAutoDetectChanges(false);
    }

    public IRepositoryEntityFramework<TEntity> GetRepository<TEntity>()
        where TEntity : class
    {
        return this.unitOfWorkInstance.GetRepository<TEntity>();
    }

    public Result SaveChanges()
    {
        this.unitOfWorkInstance.SaveChanges();

        if (this.unitOfWorkInstance.LastSaveChangesResult.IsOk)
            return Result.Ok();

        var exception = this.unitOfWorkInstance.LastSaveChangesResult.Exception!;
        var errorMessage =
            $"Unable to save changes to database | exception: {exception}";
        return Result.Fail(errorMessage);
    }
}
