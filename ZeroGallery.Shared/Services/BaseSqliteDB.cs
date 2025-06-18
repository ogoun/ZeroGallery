using SQLite;
using System.Linq.Expressions;
using ZeroGallery.Shared.Contracts;
using ZeroLevel;

namespace ZeroGallery.Shared.Services
{
    public abstract class BaseSqliteDB<T>
        : IRepository<T>
        where T : class, new()
    {
        protected SQLiteConnection _db;
        protected readonly TableQuery<T> _table;
        public BaseSqliteDB(string path, string name)
        {
            _db = new SQLiteConnection(PrepareDb(path, name));
            CreateTable();
            _table = _db.Table<T>();
        }

        public int Append(T record)
        {            
            return _db.Insert(record);
        }

        public CreateTableResult CreateTable()
        {
            return _db.CreateTable<T>();
        }

        public int DropTable()
        {
            return _db.DropTable<T>();
        }

        public IEnumerable<T> SelectAll()
        {
            return _db.Table<T>();
        }

        public IEnumerable<T> SelectBy(Expression<Func<T, bool>> predicate)
        {
            return _db.Table<T>().Where(predicate);
        }

        public T Single(Expression<Func<T, bool>> predicate)
        {
            return _db.Table<T>().FirstOrDefault(predicate);
        }

        public T Single<U>(Expression<Func<T, bool>> predicate, Expression<Func<T, U>> orderBy, bool desc = false)
        {
            if (desc)
            {
                return _db.Table<T>().Where(predicate).OrderByDescending(orderBy).FirstOrDefault();
            }
            return _db.Table<T>().Where(predicate).OrderBy(orderBy).FirstOrDefault();
        }

        public T Single<U>(Expression<Func<T, U>> orderBy, bool desc = false)
        {
            if (desc)
            {
                return _db.Table<T>().OrderByDescending(orderBy).FirstOrDefault();
            }
            return _db.Table<T>().OrderBy(orderBy).FirstOrDefault();
        }

        public IEnumerable<T> SelectBy(int N, Expression<Func<T, bool>> predicate)
        {
            return _db.Table<T>().Where(predicate).Take(N);
        }

        public long Count()
        {
            return _db.Table<T>().Count();
        }

        public long Count(Expression<Func<T, bool>> predicate)
        {
            return _db.Table<T>().Count(predicate);
        }

        public int Delete(Expression<Func<T, bool>> predicate)
        {
            return _db.Table<T>().Delete(predicate);
        }

        public int Update(T record)
        {
            return _db.Update(record);
        }

        protected static string PrepareDb(string path, string name)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                path = "db";
            }
            if (Path.IsPathRooted(path) == false)
            {
                path = Path.Combine(Configuration.BaseDirectory, path);
            }
            var result = Path.GetFullPath(path);
            Directory.CreateDirectory(result);
            return Path.Combine(result, name);
        }

        protected abstract void DisposeStorageData();

        public void Dispose()
        {
            DisposeStorageData();
            try
            {
                _db?.Close();
                _db?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[BaseSqLiteDB] Fault close db connection");
            }
        }
    }
}
