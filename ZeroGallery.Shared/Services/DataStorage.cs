using SixLabors.ImageSharp;
using ZeroGallery.Shared.Models;
using ZeroGallery.Shared.Models.DB;
using ZeroGallery.Shared.Services.DB;
using ZeroGalleryApp;
using ZeroLevel;
using ZeroLevel.Services.FileSystem;
using ZeroLevel.Services.Utils;

namespace ZeroGallery.Shared.Services
{
    /// <summary>
    /// Отвечает за хранение данных
    /// </summary>
    public class DataStorage
    {
        public const string VERSION = "1.2";
        /// <summary>
        /// Размер буфера при переносе данных из потока в поток
        /// </summary>
        private const int DEFAULT_STREAM_BUFFER_SIZE = 16384;
        /// <summary>
        /// Количество подкаталогов в хранилище
        /// </summary>
        private const int SHARDS_COUNT = 255;
        /// <summary>
        /// Локер на генерацию нового пути
        /// </summary>
        private readonly object _filePathGenerationLock = new object();
        /// <summary>
        /// Счетчики файлов в подкаталогах, для назначения имен файлов
        /// </summary>
        private readonly Dictionary<int, int> _shardCounters = new Dictionary<int, int>();
        /// <summary>
        /// Репозиторий альбомов
        /// </summary>
        private readonly DataAlbumRepository _albums;
        /// <summary>
        /// Репозиторий метаданных файлов
        /// </summary>
        private readonly DataRecordRepository _records;
        /// <summary>
        /// Каталог данных
        /// </summary>
        private readonly string _dataFolder;
        /// <summary>
        /// Каталог превью
        /// </summary>
        private readonly string _thumbsFolder;
        /// <summary>
        /// Для форматирования имени файла
        /// </summary>
        private readonly string _fileNameFormat;

        private readonly Scavenger _scavenger;

        private readonly DataPreviewProcessor _previewProcessor;

        private readonly DataConverterProcessor _convertProcessor;

        private readonly AppConfig _config;

        public DataStorage(AppConfig config,
            DataAlbumRepository albumRepository,
            DataRecordRepository recordsRepository)
        {
            if (albumRepository == null) throw new ArgumentNullException(nameof(albumRepository));
            if (recordsRepository == null) throw new ArgumentNullException(nameof(recordsRepository));

            _config = config;

            // Количество нулей в int.max
            var zerosCount = Math.Floor(Math.Log10(int.MaxValue) + 1);
            _fileNameFormat = $"d{zerosCount}";

            _dataFolder = Path.Combine(config.data_folder, "data");
            _thumbsFolder = Path.Combine(config.data_folder, "thumbs");

            if (Path.IsPathRooted(_dataFolder) == false)
            {
                _dataFolder = Path.Combine(ZeroLevel.Configuration.BaseDirectory, _dataFolder);
            }
            if (Path.IsPathRooted(_thumbsFolder) == false)
            {
                _thumbsFolder = Path.Combine(ZeroLevel.Configuration.BaseDirectory, _thumbsFolder);
            }

            Directory.CreateDirectory(_dataFolder);
            Directory.CreateDirectory(_thumbsFolder);

            _albums = albumRepository;
            _records = recordsRepository;

            LoadCounter();

            _scavenger = new Scavenger(_albums, _records, this);
            _scavenger.Run();

            _previewProcessor = new DataPreviewProcessor(_records, this);
            _previewProcessor.Run();

            _convertProcessor = new DataConverterProcessor(_config, _records, this);
            _convertProcessor.Run();
        }

        private void LoadCounter()
        {
            Log.Debug("[DataStorage.LoadCounter] Started");
            _shardCounters.Clear();
            for (int i = 0; i < SHARDS_COUNT; i++)
            {
                var folder = GetShardDataFolder(i);
                var dir = new DirectoryInfo(folder);
                if (dir.Exists)
                {
                    var names = dir.GetFiles().Select(f => f.Name);
                    if (names.Any())
                    {
                        var max_index = names.Select(s => int.Parse(s)).Max();
                        _shardCounters[i] = max_index;
                    }
                    else
                    {
                        _shardCounters[i] = 0;
                    }
                }
                else
                {
                    _shardCounters[i] = 0;
                }
            }
            Log.Debug("[DataStorage.LoadCounter] Completed");
        }

        public void DropAll()
        {
            Log.Debug("[DataStorage.DropAll] Started");
            int records_removed = _records.Delete(_ => true);
            Log.Info($"[DataStorage.DropAll] Removed '{records_removed}' data records");
            int albums_removed = _albums.Delete(_ => true);
            Log.Info($"[DataStorage.DropAll] Removed '{albums_removed}' album records");
            FSUtils.CleanAndTestFolder(_dataFolder);
            FSUtils.CleanAndTestFolder(_thumbsFolder);
            LoadCounter();
            Log.Debug("[DataStorage.DropAll] Complete");
        }

        /// <summary>
        /// Удаление записи
        /// </summary>
        /// <param name="recordId"></param>
        public void RemoveRecord(long recordId, bool isAdmin)
        {
            Log.Debug($"[DataStorage.RemoveRecord] '{recordId}' started{(isAdmin ? " as admin" : string.Empty)}");
            var record = _records.SelectBy(r => r.Id == recordId)?.FirstOrDefault();
            if (record != null)
            {
                try
                {
                    if (record.AlbumId != -1)
                    {
                        var album = _albums.Single(a => a.Id == record.AlbumId);
                        if (album.AllowRemoveData == false && isAdmin == false)
                        {
                            Log.Error($"[DataStorage.RemoveRecord] Fault mark record '{record.Id}' for removing. Delete files from album '{record.AlbumId}' not allowed.");
                            throw new Exception("Delete files from album not allowed");
                        }
                    }
                    record.InRemoving = true;
                    _records.Update(record);
                    Log.Info($"[DataStorage.RemoveRecord] Record '{record.Id}' marked for removing");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"[DataStorage.RemoveRecord] Failed to mark the record '{recordId}' for removing");
                }
            }
            else
            {
                throw new Exception($"File '{recordId}' not found");
            }
            Log.Debug($"[DataStorage.RemoveRecord] '{recordId}'{(isAdmin ? " as admin" : string.Empty)} completed");
        }

        /// <summary>
        /// Удаление альбома
        /// </summary>
        public void RemoveAlbum(long albumId, bool isAdmin)
        {
            Log.Debug($"[DataStorage.RemoveAlbum] '{albumId}' started{(isAdmin ? " as admin" : string.Empty)}");
            var album = _albums.SelectBy(r => r.Id == albumId)?.FirstOrDefault();
            if (album != null)
            {
                if (album.AllowRemoveData == false && isAdmin == false)
                {
                    Log.Error($"[DataStorage.RemoveAlbum] Fault mark album '{albumId}' for removing. Delete files from album '{albumId}' not allowed.");
                    throw new Exception("Delete files from album not allowed");
                }

                var albumRecords = _records.SelectBy(r => r.AlbumId == albumId)?.ToList();
                if (albumRecords != null)
                {
                    foreach (var record in albumRecords)
                    {
                        record.InRemoving = true;
                        _records.Update(record);
                        Log.Info($"[DataStorage.RemoveAlbum] Data record '{record.Id}' marked for removing");
                    }
                }
                album.InRemoving = true;
                _albums.Update(album);
                Log.Info($"[DataStorage.RemoveAlbum] Album '{albumId}' marked for removing");
            }
            else
            {
                throw new Exception($"Album '{albumId}' not found");
            }
            Log.Debug($"[DataStorage.RemoveAlbum] '{albumId}'{(isAdmin ? " as admin" : string.Empty)} completed");
        }

        /// <summary>
        /// Создание альбома
        /// </summary>
        public DataAlbum AppendAlbum(string name, string description, string token, bool allowRemoveData)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }
            var album = new DataAlbum
            {
                Name = name,
                Description = description,
                Token = token,
                AllowRemoveData = allowRemoveData,
            };
            var r = _albums.AppendAndGet(album);
            Log.Info($"[DataStorage.AppendAlbum] Created album '{r.Id}' ({r.Name})");
            return r;
        }

        /// <summary>
        /// Запись файла
        /// </summary>
        public async Task<DataRecord> WriteData(string name, string description,
            string tags, long albumId, Stream dataStream)
        {
            Log.Debug($"[DataStorage.WriteData] Started. Name: '{name ?? string.Empty}'. AlbumId: {albumId}.");

            var timestamp = Timestamp.UtcNow;
            var shardIndex = (int)(timestamp % SHARDS_COUNT);
            var (fileIndex, dataFilePath, thumbFilePath) = GetNewRelativePath(shardIndex);
            long fileSize;
            ImageTypeInfo imageInfo;

            PreviewState previewState = PreviewState.WAITING;
            ConvertDataState convertState = ConvertDataState.WAITING;

            using (var storeStream = new FileStream(dataFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, DEFAULT_STREAM_BUFFER_SIZE))
            {
                fileSize = await StreamHelper.Transfer(dataStream, storeStream);
                imageInfo = MediaTypeDetector.GetDataTypeInfo(storeStream);
                await storeStream.FlushAsync();
                storeStream.Close();
            }

            if (imageInfo.IsVideo())
            {
                if (imageInfo.Extension.IsEqual(".mp4") || _config.convert_video_to_mp4 == false)
                {
                    convertState = ConvertDataState.COMPLETED;
                }
            }
            else if (imageInfo.IsImage())
            {
                switch (imageInfo.Extension)
                {
                    case ".heic":
                        if (!_config.convert_heic_to_jpg)
                        {
                            convertState = ConvertDataState.COMPLETED;
                        }
                        break;
                    case ".tiff":
                        if (!_config.convert_tiff_to_jpg)
                        {
                            convertState = ConvertDataState.COMPLETED;
                        }
                        break;
                    default:
                        convertState = ConvertDataState.COMPLETED;
                        break;
                }
            }
            else
            {
                previewState = PreviewState.NO_PREVIEW;
                convertState = ConvertDataState.COMPLETED;
            }
            var record = new DataRecord
            {
                CreatedTimestamp = timestamp,
                Description = description,
                Name = name ?? string.Empty,
                AlbumId = albumId,
                Index = fileIndex,
                ShardIndex = shardIndex,
                Size = fileSize,
                Tags = tags ?? string.Empty,
                MimeType = imageInfo.MimeType,
                Extension = imageInfo.Extension,
                ConvertStatus = (int)convertState,
                PreviewStatus = (int)previewState,
            };
            Log.Info($"[DataStorage.WriteData] Data stored. Id: {record.Id} Name: '{name ?? string.Empty}'. AlbumId: {albumId}.");
            return _records.AppendAndGet(record);
        }

        /// <summary>
        /// Запрос альбомов
        /// </summary>
        public IEnumerable<DataAlbum> GetAlbums()
        {
            Log.Debug("[DataStorage.GetAlbums]");
            return _albums.SelectBy(r => r.InRemoving == false);
        }

        /// <summary>
        /// Запрос всех данных
        /// </summary>
        public IEnumerable<DataRecord> GetAllData()
        {
            Log.Debug("[DataStorage.GetAllData]");
            return _records.SelectBy(r => r.InRemoving == false);
        }

        /// <summary>
        /// Запрос всех данных вне альбомов
        /// </summary>
        public IEnumerable<DataRecord> GetDataWithoutAlbums()
        {
            Log.Debug("[DataStorage.GetDataWithoutAlbums]");
            return _records.SelectBy(r => r.AlbumId == -1 && r.InRemoving == false);
        }

        /// <summary>
        /// Запрос данных конкретного альбома
        /// </summary>
        /// <param name="albumId"></param>
        public IEnumerable<DataRecord> GetDataByAlbum(long albumId)
        {
            Log.Debug($"[DataStorage.GetDataByAlbum] Album '{albumId}'");
            return _records.SelectBy(r => r.AlbumId == albumId && r.InRemoving == false);
        }

        /// <summary>
        /// Запрос превью для файла данных
        /// </summary>
        public DataFileInfo GetPreview(long id)
        {
            Log.Debug($"[DataStorage.GetPreview] Record '{id}'");
            var rec = _records.Single(c => c.Id == id && c.InRemoving == false);
            return GetPreview(rec);
        }

        /// <summary>
        /// Запрос файла данных
        /// </summary>
        public DataFileInfo GetData(long id)
        {
            Log.Debug($"[DataStorage.GetData] Record '{id}'");
            var rec = _records.Single(c => c.Id == id && c.InRemoving == false);
            return GetData(rec);
        }

        /// <summary>
        /// Получение токена для доступа к альбому
        /// </summary>
        public string GetAlbumToken(long albumId)
        {
            if (albumId == -1) return string.Empty;
            var album = _albums.Single(a => a.Id == albumId);
            if (album == null) throw new Exception($"Album '{albumId}' does not exists");
            return album.Token!;
        }

        /// <summary>
        /// Получение токена для доступа к файлу, если он относится к альбому
        /// </summary>
        public string GetItemAlbumToken(long id)
        {
            var rec = _records.Single(c => c.Id == id && c.InRemoving == false);
            if (rec != null)
            {
                if (rec.AlbumId == -1) return string.Empty;
                var album = _albums.Single(a => a.Id == rec.AlbumId);
                if (album == null) throw new Exception($"Album '{rec.AlbumId}' does not exists");
                return album.Token!;
            }
            throw new Exception($"File '{id}' does not exists");
        }


        internal string GetPreviewPath(DataRecord rec)
        {
            if (rec != null)
            {
                var relativePath = GetRelativePath(rec);
                if (string.IsNullOrWhiteSpace(relativePath) == false)
                {
                    return Path.Combine(_thumbsFolder, relativePath);
                }
            }
            return default!;
        }

        internal DataFileInfo GetData(DataRecord rec)
        {
            var relativePath = GetRelativePath(rec);
            if (string.IsNullOrWhiteSpace(relativePath) == false)
            {
                var path = Path.Combine(_dataFolder, relativePath);
                if (File.Exists(path))
                {
                    return new DataFileInfo(path, rec.Name, rec.MimeType, GetDataType(rec));
                }
            }
            return default!;
        }

        internal DataFileInfo GetPreview(DataRecord rec)
        {
            if (rec != null)
            {
                if (rec.PreviewStatus == (int)PreviewState.HAS_PREVIEW)
                {
                    var relativePath = GetRelativePath(rec);
                    if (string.IsNullOrWhiteSpace(relativePath) == false)
                    {
                        var path = Path.Combine(_thumbsFolder, relativePath);
                        if (File.Exists(path))
                        {
                            return new DataFileInfo(path, rec.Name, "image/jpeg", GetDataType(rec));
                        }
                    }
                }
                return new DataFileInfo(null!, rec.Name, "image/jpeg", GetDataType(rec));
            }
            return default!;
        }

        /// <summary>
        /// Получение относительного пути к файлу
        /// </summary>
        private string GetRelativePath(DataRecord rec)
        {
            if (rec != null)
            {
                return Path.Combine(rec.ShardIndex.ToString("d3"), rec.Index.ToString(_fileNameFormat));
            }
            return default!;
        }
        /// <summary>
        /// Получение путей для нового файла данных
        /// </summary>
        private (int, string, string) GetNewRelativePath(int shardIndex)
        {
            var fileIndex = 0;
            lock (_filePathGenerationLock)
            {
                fileIndex = _shardCounters[shardIndex] + 1;
                _shardCounters[shardIndex] = fileIndex;
            }
            var dataFolder = GetShardDataFolder(shardIndex);
            var thumbFolder = GetShardThumbsFolder(shardIndex);

            Directory.CreateDirectory(dataFolder);
            Directory.CreateDirectory(thumbFolder);

            var dataPath = Path.Combine(dataFolder, fileIndex.ToString(_fileNameFormat));
            var thumbPath = Path.Combine(thumbFolder, fileIndex.ToString(_fileNameFormat));
            return (fileIndex, dataPath, thumbPath);
        }
        /// <summary>
        /// Путь к каталогу для файлов данных
        /// </summary>
        private string GetShardDataFolder(int shardIndex) => Path.Combine(_dataFolder, shardIndex.ToString("d3"));
        /// <summary>
        /// Путь к каталогу для хранения превью
        /// </summary>
        private string GetShardThumbsFolder(int shardIndex) => Path.Combine(_thumbsFolder, shardIndex.ToString("d3"));
        /// <summary>
        /// Определение типа данных
        /// </summary>
        private static DataType GetDataType(DataRecord rec)
        {
            if (KnownImages.IsImage(rec.Extension)) return DataType.Image;
            if (KnownVideos.IsVideo(rec.Extension)) return DataType.Video;
            return DataType.Binary;
        }
    }
}
