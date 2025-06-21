using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using ZeroGallery.Shared.Models;
using ZeroGallery.Shared.Models.DB;
using ZeroGallery.Shared.Services.DB;
using ZeroLevel;

namespace ZeroGallery.Shared.Services
{
    public sealed class DataPreviewProcessor
        : IDisposable
    {
        private const int MAX_PREVIEW_SIDE_SIZE = 512;

        private readonly IImageConverter _imageConverter;
        private readonly DataRecordRepository _records;
        private readonly DataStorage _storage;
        
        public DataPreviewProcessor(DataRecordRepository recordsRepository, DataStorage storage)
        {
            _records = recordsRepository;
            _storage = storage;
            _imageConverter = new UnifiedImageConverter();
        }

        public void Run()
        {
            Sheduller.RemindEvery(TimeSpan.FromSeconds(30), async () => await Collect());
        }

        private async Task Collect()
        {
            foreach (var record in _records.GetWaitingPreviewRecords())
            {
                try
                {
                    if (KnownImages.IsImage(record.Extension))
                    {
                        await CreatePreviewForImage(record);
                    }
                    else if (KnownVideos.IsVideo(record.Extension))
                    {
                        await CreatePreviewForVideo(record);
                    }
                    else
                    {
                        record.PreviewStatus = (int)PreviewState.NO_PREVIEW;
                        _records.Update(record);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"[DataPreviewProcessor.Collect] Fault create preview for record '{record.Id}'");
                }
            }
        }

        private async Task CreatePreviewForImage(DataRecord record)
        {
            byte[] jpgData;

            var data = _storage.GetData(record);
            var thumbFilePath = _storage.GetPreviewPath(record);

            using (var dataStream = File.OpenRead(data.FilePath))
            {
                if (record.Extension != ImageTypeInfo.DEFAULT_IMAGE_EXTENSION)
                {
                    jpgData = await _imageConverter.ConvertToJpgAsync(dataStream, record.Extension);
                }
                else
                {
                    using (var ms = new MemoryStream())
                    {
                        StreamHelper.Transfer(dataStream, ms).Wait();
                        ms.Position = 0;
                        jpgData = ms.ToArray();
                    }
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

            record.PreviewStatus = (int)PreviewState.HAS_PREVIEW;
            _records.Update(record);
        }

        private async Task CreatePreviewForVideo(DataRecord record)
        {
            // Ожидание пока видео переконвертируется
            if (record.ConvertStatus == (int)ConvertDataState.WAITING)
                return;

            var data = _storage.GetData(record);
            var dataFilePath = data.FilePath;
            var thumbFilePath = _storage.GetPreviewPath(record);

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
                record.PreviewStatus = (int)PreviewState.HAS_PREVIEW;
                _records.Update(record);
            }
            finally
            {
                if (!File.Exists(dataFilePath) && File.Exists(tempFileSource))
                {
                    File.Move(tempFileSource, dataFilePath, true);
                }
            }
        }

        public void Dispose()
        {
            _imageConverter?.Dispose();
        }
    }
}
