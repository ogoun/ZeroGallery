using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
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
        : IDisposable
    {
        public const string VERSION = "1.0";

        /// <summary>
        /// Максимальный размер стороны изображения для превью
        /// </summary>
        private const int MAX_PREVIEW_SIDE_SIZE = 512;
        /// <summary>
        /// Количество подкаталогов в хранилище
        /// </summary>
        private const int SHARDS_COUNT = 255;
        /// <summary>
        /// Размер буфера при переносе данных из потока в поток
        /// </summary>
        protected const int DEFAULT_STREAM_BUFFER_SIZE = 16384;
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
        /// <summary>
        /// Конвертация изображений в jpg формат из других форматов для превью
        /// </summary>
        private readonly IImageConverter _imageConverter;

        private readonly Scavenger _scavenger;

        public DataStorage(AppConfig config,
            DataAlbumRepository albumRepository,
            DataRecordRepository recordsRepository)
        {
            if (albumRepository == null) throw new ArgumentNullException(nameof(albumRepository));
            if (recordsRepository == null) throw new ArgumentNullException(nameof(recordsRepository));

            // Количество нулей в int.max
            var zerosCount = Math.Floor(Math.Log10(int.MaxValue) + 1);
            _fileNameFormat = $"d{zerosCount}";

            _dataFolder = Path.Combine(config.data_folder, "data");
            _thumbsFolder = Path.Combine(config.data_folder, "thumbs");
            _imageConverter = new UnifiedImageConverter();

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
        }

        private void LoadCounter()
        {
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
        }

        public void DropAll()
        {
            _records.Delete(_ => true);
            _albums.Delete(_ => true);
            FSUtils.CleanAndTestFolder(_dataFolder);
            FSUtils.CleanAndTestFolder(_thumbsFolder);
            LoadCounter();
        }

        /// <summary>
        /// Удаление записи
        /// </summary>
        /// <param name="recordId"></param>
        public void RemoveRecord(long recordId, bool isAdmin)
        {
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
                            throw new Exception("Delete files from album not allowed");
                        }
                    }
                    record.InRemoving = true;
                    _records.Update(record);
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
        }

        /// <summary>
        /// Удаление альбома
        /// </summary>
        public void RemoveAlbum(long albumId, bool isAdmin)
        {
            var album = _albums.SelectBy(r => r.Id == albumId)?.FirstOrDefault();
            if (album != null)
            {
                if (album.AllowRemoveData == false && isAdmin == false)
                {
                    throw new Exception("Delete files from album not allowed");
                }

                var albumRecords = _records.SelectBy(r => r.AlbumId == albumId)?.ToList();
                if (albumRecords != null)
                {
                    foreach (var record in albumRecords)
                    {
                        record.InRemoving = true;
                        _records.Update(record);
                    }
                }
                album.InRemoving = true;
                _albums.Update(album);
            }
            else
            {
                throw new Exception($"Album '{albumId}' not found");
            }
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
            return r;
        }

        /// <summary>
        /// Запись файла
        /// </summary>
        public async Task<DataRecord> WriteData(string name, string description,
            string tags, long albumId, Stream dataStream)
        {
            var timestamp = Timestamp.UtcNow;
            var shardIndex = (int)(timestamp % SHARDS_COUNT);
            var (fileIndex, dataFilePath, thumbFilePath) = GetNewRelativePath(shardIndex);
            long fileSize;
            ImageTypeInfo imageInfo;
            using (var storeStream = new FileStream(dataFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, DEFAULT_STREAM_BUFFER_SIZE))
            {
                fileSize = await Transfer(dataStream, storeStream);
                imageInfo = MediaTypeDetector.GetDataTypeInfo(storeStream);

                storeStream.Position = 0;

                // Preview ------------------------
                if (imageInfo.IsImage())
                {
                    byte[] jpgData;
                    if (imageInfo.Extension != ImageTypeInfo.DEFAULT_IMAGE_EXTENSION)
                    {
                        jpgData = await _imageConverter.ConvertToJpgAsync(storeStream, imageInfo.Extension);
                    }
                    else
                    {
                        using (var ms = new MemoryStream())
                        {
                            await Transfer(storeStream, ms);
                            ms.Position = 0;
                            jpgData = ms.ToArray();
                        }
                    }

                    using (var image = Image.Load(jpgData))
                    {
                        if (image.Width > MAX_PREVIEW_SIDE_SIZE ||
                            image.Height > MAX_PREVIEW_SIDE_SIZE)
                        {
                            var max_side = (float)Math.Max(image.Width, image.Height);
                            var k = (float)MAX_PREVIEW_SIDE_SIZE / max_side;

                            var w = (int)(image.Width * k);
                            var h = (int)(image.Height * k);

                            image.Mutate(i => i.Resize(w, h));
                        }
                        image.SaveAsJpeg(thumbFilePath);
                    }
                }
                // Preview ------------------------
            }

            // Convert .wmv and .avi  to .mp4 for suport in modern browsers
            if (imageInfo.IsVideo())
            {
                try
                {
                    switch (imageInfo.Extension)
                    {
                        case ".wmv":
                        case ".avi":
                        case ".mov":
                        case ".mkv":
                            var output = FSUtils.GetAppLocalTemporaryFile() + ".mp4";
                            var succsessfully_convert = await VideoConverter.ConvertToMp4Async(dataFilePath, output);
                            if (succsessfully_convert)
                            {
                                File.Move(output, dataFilePath, true);
                                imageInfo.Extension = ".mp4";
                                imageInfo.MimeType = "video/mp4";
                            }
                            else
                            {
                                Log.Warning("[DataStorage.WriteData] Can't convert video file to .mp4");
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[DataStorage.WriteData] Fault convert video file to .mp4");
                }

                // Preview к видео
                var tempFileSource = dataFilePath + ".mp4"; // т.к. ffmpeg не осиливает файлы без расширения
                var tempFileOutput = thumbFilePath + ".jpg";
                try
                {
                    File.Move(dataFilePath, tempFileSource, true);
                    if (await VideoThumbnailService.GenerateThumbnailAsync(tempFileSource, tempFileOutput))
                    {
                        File.Move(tempFileOutput, thumbFilePath, true);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[DataStorage.WriteData] Fault create preview for video file");
                }
                finally
                {
                    if (!File.Exists(dataFilePath) && File.Exists(tempFileSource))
                    {
                        File.Move(tempFileSource, dataFilePath, true);
                    }
                }
            }

            var record = new DataRecord
            {
                CreatedTimestamp = timestamp,
                Description = description,
                Name = name,
                AlbumId = albumId,
                Index = fileIndex,
                ShardIndex = shardIndex,
                Size = fileSize,
                Tags = tags ?? string.Empty,
                MimeType = imageInfo.MimeType,
                Extension = imageInfo.Extension,
            };
            return _records.AppendAndGet(record);
        }

        /// <summary>
        /// Запрос альбомов
        /// </summary>
        public IEnumerable<DataAlbum> GetAlbums()
        {
            return _albums.SelectBy(r => r.InRemoving == false);
        }

        /// <summary>
        /// Запрос всех данных
        /// </summary>
        public IEnumerable<DataRecord> GetAllData()
        {
            return _records.SelectBy(r => r.InRemoving == false);
        }

        /// <summary>
        /// Запрос всех данных вне альбомов
        /// </summary>
        public IEnumerable<DataRecord> GetDataWithoutAlbums()
        {
            return _records.SelectBy(r => r.AlbumId == -1 && r.InRemoving == false);
        }

        /// <summary>
        /// Запрос данных конкретного альбома
        /// </summary>
        /// <param name="albumId"></param>
        public IEnumerable<DataRecord> GetDataByAlbum(long albumId)
        {
            return _records.SelectBy(r => r.AlbumId == albumId && r.InRemoving == false);
        }

        /// <summary>
        /// Запрос превью для файла данных
        /// </summary>
        public DataFileInfo GetPreview(long id)
        {
            var rec = _records.Single(c => c.Id == id && c.InRemoving == false);
            return GetPreview(rec);
        }

        public DataFileInfo GetPreview(DataRecord rec)
        {
            if (rec != null)
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
                return new DataFileInfo(null!, rec.Name, "image/jpeg", GetDataType(rec));
            }
            return default!;
        }

        /// <summary>
        /// Запрос файла данных
        /// </summary>
        public DataFileInfo GetData(long id)
        {
            var rec = _records.Single(c => c.Id == id && c.InRemoving == false);
            return GetData(rec);
        }

        public DataFileInfo GetData(DataRecord rec)
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
        /// Копирование данных из потока в поток
        /// </summary>
        protected static async Task<long> Transfer(Stream input, Stream output)
        {
            if (input.CanRead == false)
            {
                throw new InvalidOperationException("Input stream can not be read.");
            }
            if (output.CanWrite == false)
            {
                throw new InvalidOperationException("Output stream can not be write.");
            }
            long totalBytes = 0;
            var readed = 0;
            var buffer = new byte[DEFAULT_STREAM_BUFFER_SIZE];
            while ((readed = input.Read(buffer, 0, buffer.Length)) != 0)
            {
                await output.WriteAsync(buffer, 0, readed);
                totalBytes += readed;
            }
            await output.FlushAsync();
            return readed;
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

        public void Dispose()
        {
            _imageConverter?.Dispose();
        }
    }
}
