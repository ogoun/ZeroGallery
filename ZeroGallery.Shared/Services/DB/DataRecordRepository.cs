using ZeroGallery.Shared.Models.DB;

namespace ZeroGallery.Shared.Services.DB
{
    internal class DataRecordRepository
        : BaseSqliteDB<DataRecord>
    {
        private readonly object _locker = new object();
        public DataRecordRepository()
            : base("data")
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

        protected override void DisposeStorageData()
        {
        }
    }
}
