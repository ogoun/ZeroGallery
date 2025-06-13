using System.Linq.Expressions;

namespace ZeroGallery.Shared.Contracts
{
    public interface IRepository<T>
        : IDisposable
    {
        int Append(T record);
        int DropTable();
        IEnumerable<T> SelectAll();
        IEnumerable<T> SelectBy(Expression<Func<T, bool>> predicate);
        T Single(Expression<Func<T, bool>> predicate);
        T Single<U>(Expression<Func<T, bool>> predicate, Expression<Func<T, U>> orderBy, bool desc = false);
        T Single<U>(Expression<Func<T, U>> orderBy, bool desc = false);
        IEnumerable<T> SelectBy(int N, Expression<Func<T, bool>> predicate);
        long Count();
        long Count(Expression<Func<T, bool>> predicate);
        int Delete(Expression<Func<T, bool>> predicate);
        int Update(T record);

    }
}
