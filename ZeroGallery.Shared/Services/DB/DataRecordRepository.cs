using ZeroGallery.Shared.Models.DB;

namespace ZeroGallery.Shared.Services.DB
{
    public sealed class DataRecordRepository
        : BaseSqliteDB<DataRecord>
    {
        private readonly object _locker = new object();
        public DataRecordRepository(string db_root)
            : base(db_root, "data")
        {
        }

        public DataRecord AppendAndGet(DataRecord record)
        {
            lock (_locker)
            {
                var r = _db.Insert(record);
                return _table.MaxBy(r => r.Id)!;
            }
        }

        public IEnumerable<DataRecord> GetRemovingRecords() => SelectBy(r => r.InRemoving == true) ?? Enumerable.Empty<DataRecord>();

        public IEnumerable<DataRecord> GetWaitingPreviewRecords() => SelectBy(r => r.InRemoving == false && r.PreviewStatus == 0) ?? Enumerable.Empty<DataRecord>();

        public IEnumerable<DataRecord> GetWaitingConvertRecords() => SelectBy(r => r.InRemoving == false && r.ConvertStatus == 0) ?? Enumerable.Empty<DataRecord>();

        public long GetAlbumFilesCount(long albumId) => Count(r => r.AlbumId == albumId);

        protected override void DisposeStorageData()
        {
        }
    }
}
