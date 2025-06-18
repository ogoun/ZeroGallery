using ZeroGallery.Shared.Models.DB;

namespace ZeroGallery.Shared.Services.DB
{
    public sealed class DataAlbumRepository
        : BaseSqliteDB<DataAlbum>
    {
        private readonly object _locker = new object();
        public DataAlbumRepository(string db_root)
            : base(db_root, "groups")
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

        public IEnumerable<DataAlbum> GetRemovingRecords() => SelectBy(r => r.InRemoving == true) ?? Enumerable.Empty<DataAlbum>();

        protected override void DisposeStorageData()
        {
        }
    }
}
