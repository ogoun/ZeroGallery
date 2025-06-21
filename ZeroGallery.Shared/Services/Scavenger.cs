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
            Sheduller.RemindEvery(TimeSpan.FromSeconds(10), CollectRemovedRecords);
            Sheduller.RemindEvery(TimeSpan.FromHours(3), CollectMissedFilesRecords);
        }

        private void CollectRemovedRecords()
        {
            foreach (var record in _records.GetRemovingRecords())
            {
                try
                {
                    if (HandleRecord(record))
                    {
                        Log.Info($"[Scavenger.Collect] Record '{record.Id}' removed");
                    }
                    else
                    {
                        Log.Warning($"[Scavenger.Collect] Delete record '{record.Id}' method return 0 as count of deleted records.");
                    }
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
                    if (_albums.Delete(a => a.Id == album.Id) > 0)
                    {
                        Log.Info($"[Scavenger.Collect] Album '{album.Id}' removed");
                    }
                    else
                    {
                        Log.Warning($"[Scavenger.Collect] Delete album '{album.Id}' method return 0 as count of deleted records.");
                    }
                }
            }
        }

        private void CollectMissedFilesRecords()
        {
            foreach (var record in _records.SelectBy(r => r.InRemoving == false))
            {
                try
                {
                    var dataFile = _storage.GetData(record);
                    if (File.Exists(dataFile.FilePath) == false)
                    {
                        if (record.PreviewStatus == (int)PreviewState.HAS_PREVIEW)
                        {
                            var previewFilePath = _storage.GetPreviewPath(record);
                            if (File.Exists(previewFilePath))
                            {
                                File.Delete(previewFilePath);
                            }
                        }
                        _records.Delete(r=>r.Id == record.Id);
                        Log.Warning($"[Scavenger] Found record without data file. Record '{record.Id}' removed. ({record.Name})");
                    }
                    else if (record.PreviewStatus == (int)PreviewState.HAS_PREVIEW)
                    {                        
                        var previewFile = _storage.GetPreview(record);
                        if (File.Exists(previewFile.FilePath) == false)
                        {
                            record.PreviewStatus = (int)PreviewState.WAITING;
                            _records.Update(record);
                            Log.Warning($"[Scavenger] Found record without preview file. Recreate preview for record '{record.Id}'. ({record.Name})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"[Scavenger.CollectMissedFilesRecords] Fault check files for record '{record.Id}'");
                }
            }
        }

        private bool HandleRecord(DataRecord record)
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
            return _records.Delete(r => r.Id == record.Id) > 0;
        }
    }
}
