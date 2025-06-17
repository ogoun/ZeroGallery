using ZeroGallery.Shared.Models.DB;
using ZeroGallery.Shared.Services.DB;
using ZeroLevel;

namespace ZeroGallery.Shared.Services
{
    /// <summary>
    /// Подчищает мусор
    /// </summary>
    public sealed class Scavenger
    {
        /// <summary>
        /// Репозиторий альбомов
        /// </summary>
        private readonly DataAlbumRepository _albums;
        /// <summary>
        /// Репозиторий метаданных файлов
        /// </summary>
        private readonly DataRecordRepository _records;

        private readonly DataStorage _storage;

        public Scavenger(DataAlbumRepository albumRepository, DataRecordRepository recordsRepository, DataStorage storage)
        {
            _albums = albumRepository;
            _records = recordsRepository;
            _storage = storage;
        }

        public void Run()
        {
            Sheduller.RemindEvery(TimeSpan.FromSeconds(10), Collect);
        }

        private void Collect()
        {
            foreach (var record in _records.GetRemovingRecords())
            {
                try
                {
                    HandleRecord(record);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"[Scavenger.Collect] Fault remove files for record '{record.Id}'");
                }
            }
            foreach (var album in _albums.GetRemovingRecords())
            {
                if (_records.GetAlbumFilesCount(album.Id) == 0)
                {
                    _albums.Delete(a => a.Id == album.Id);
                }
            }
        }

        private void HandleRecord(DataRecord record)
        {
            var dataFile = _storage.GetData(record);
            var previewFile = _storage.GetPreview(record);
            if (File.Exists(dataFile.FilePath))
            {
                File.Delete(dataFile.FilePath);
            }
            if (File.Exists(previewFile.FilePath))
            {
                File.Delete(previewFile.FilePath);
            }
            _records.Delete(r => r.Id == record.Id);
        }
    }
}
