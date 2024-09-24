//namespace BridgingIT.DevKit.Domain.Repositories.QueryBuilder;

//using BridgingIT.DevKit.Domain.Model;

//public interface IQueryBuilderFactory
//{
//    IQueryBuilder Create<TEntity>(IGenericReadOnlyRepository<TEntity> repository)
//        where TEntity : class, IEntity;
//}

//public class DefaultQueryBuilderFactory : IQueryBuilderFactory
//{
//    public IQueryBuilder Create<TEntity>(IGenericReadOnlyRepository<TEntity> repository)
//        where TEntity : class, IEntity
//    {
//        // Assuming a generic QueryBuilder implementation exists that can handle any IEntity
//        return new DefaultQueryBuilder<TEntity>(repository);
//    }
//}

//public interface IQueryBuilder
//{
//}

//public class DefaultQueryBuilder<TEntity>(IGenericReadOnlyRepository<TEntity> repository) : IQueryBuilder
//    where TEntity : class, IEntity
//{
//}

