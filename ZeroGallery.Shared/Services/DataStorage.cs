using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using ZeroGallery.Shared.Models;
using ZeroGallery.Shared.Models.DB;
using ZeroGallery.Shared.Services.DB;
using ZeroGalleryApp;
using ZeroLevel.Services.FileSystem;
using ZeroLevel.Services.Utils;

namespace ZeroGallery.Shared.Services
{
    public class DataStorage
        : IDisposable
    {
        private const int MAX_PREVIEW_SIDE_SIZE = 512;
        private const int SHARDS_COUNT = 255;
        protected const int DEFAULT_STREAM_BUFFER_SIZE = 16384;
        private readonly object _filePathGenerationLock = new object();

        private readonly Dictionary<int, int> _shardCounters = new Dictionary<int, int>();

        private readonly DataAlbumRepository _albums = new DataAlbumRepository();
        private readonly DataRecordRepository _records = new DataRecordRepository();

        private readonly string _dataFolder;
        private readonly string _thumbsFolder;

        private readonly string _fileNameFormat;

        private readonly IImageConverter _imageConverter;

        public DataStorage(AppConfig config)
        {
            var zerosCount = Math.Floor(Math.Log10(int.MaxValue) + 1);
            _fileNameFormat = $"d{zerosCount}";

            _dataFolder = Path.Combine(config.data_folder, "data");
            _thumbsFolder = Path.Combine(config.data_folder, "thumbs");
            _imageConverter = new UnifiedImageConverter();

            Directory.CreateDirectory(_dataFolder);
            Directory.CreateDirectory(_thumbsFolder);
            LoadCounter();
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

        public DataAlbum AppendAlbum(string name, string description, string token)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }
            var album = new DataAlbum
            {
                Name = name,
                Description = description,
                Token = token
            };
            var r = _albums.AppendAndGet(album);
            return r;
        }

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
            return _albums.SelectAll();
        }

        /// <summary>
        /// Запрос всех данных
        /// </summary>
        public IEnumerable<DataRecord> GetAllData()
        {
            return _records.SelectAll();
        }

        /// <summary>
        /// Запрос всех данных вне альбомов
        /// </summary>
        public IEnumerable<DataRecord> GetDataWithoutAlbums()
        {
            return _records.SelectBy(r => r.AlbumId == -1);
        }

        /// <summary>
        /// Запрос данных конкретного альбома
        /// </summary>
        /// <param name="albumId"></param>
        public IEnumerable<DataRecord> GetDataByAlbum(long albumId)
        {
            return _records.SelectBy(r => r.AlbumId == albumId);
        }

        /// <summary>
        /// Запрос превью для файла данных
        /// </summary>
        public DataFileInfo GetPreview(long id)
        {
            var rec = _records.Single(c => c.Id == id);
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
            var rec = _records.Single(c => c.Id == id);
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
        /// Проверка наличия доступа к альбому
        /// </summary>
        public bool HasAccessToAlbum(long albumId, string token)
        {
            var album = _albums.Single(a => a.Id == albumId);
            return album?.Token.IsEqual(token) ?? false;
        }

        /// <summary>
        /// Проверка наличия доступа к файлу
        /// </summary>
        public bool HasAccessToItem(long id, string token)
        {
            var rec = _records.Single(c => c.Id == id);
            if (rec != null)
            {
                if (rec.AlbumId == -1) return true;
                var album = _albums.Single(a => a.Id == rec.AlbumId);
                if (album != null)
                {
                    return album.Token.IsEqual(token);
                }
            }
            return false;
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
