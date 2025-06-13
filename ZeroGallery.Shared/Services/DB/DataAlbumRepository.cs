using ZeroGallery.Shared.Models.DB;

namespace ZeroGallery.Shared.Services.DB
{
    internal class DataAlbumRepository
        : BaseSqliteDB<DataAlbum>
    {
        private readonly object _locker = new object();
        public DataAlbumRepository()
            : base("groups")
        {
        }

        public DataAlbum AppendAndGet(DataAlbum record)
        {
            lock (_locker)
            {
                var r = _db.Insert(record);
                return _table.MaxBy(r => r.Id)!;
            }
        }

        protected override void DisposeStorageData()
        {
        }
    }
}
