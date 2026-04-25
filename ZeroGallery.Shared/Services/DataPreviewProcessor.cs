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
        private int _running;

        public DataPreviewProcessor(DataRecordRepository recordsRepository, DataStorage storage)
        {
            _records = recordsRepository;
            _storage = storage;
            _imageConverter = new UnifiedImageConverter();
        }

        public void Run()
        {
            Sheduller.RemindEvery(TimeSpan.FromSeconds(30), async () =>
            {
                if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return;
                try { await Collect(); }
                finally { Interlocked.Exchange(ref _running, 0); }
            });
        }

        private async Task Collect()
        {
            try
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
            catch (Exception ex)
            {
                Log.Error(ex, "[DataPreviewProcessor.Collect] Outer failure");
            }
        }

        private async Task CreatePreviewForImage(DataRecord record)
        {
            var data = _storage.GetData(record);
            var thumbFilePath = _storage.GetPreviewPath(record);

            Image image;
            if (record.Extension != ImageTypeInfo.DEFAULT_IMAGE_EXTENSION)
            {
                byte[] jpgData;
                using (var dataStream = File.OpenRead(data.FilePath))
                {
                    jpgData = await _imageConverter.ConvertToJpgAsync(dataStream, record.Extension);
                }
                image = Image.Load(jpgData);
            }
            else
            {
                using var dataStream = File.OpenRead(data.FilePath);
                image = await Image.LoadAsync(dataStream);
            }

            using (image)
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
                await image.SaveAsJpegAsync(thumbFilePath);
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
            if (data == null) return;

            var dataFilePath = data.FilePath;
            var thumbFilePath = _storage.GetPreviewPath(record);

            // ffmpeg требует расширение у входного файла, поэтому работаем с копией
            var tempFileSource = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".mp4");
            var tempFileOutput = tempFileSource + ".jpg";
            try
            {
                File.Copy(dataFilePath, tempFileSource, true);
                if (await VideoThumbnailService.GenerateThumbnailAsync(tempFileSource, tempFileOutput))
                {
                    File.Move(tempFileOutput, thumbFilePath, true);
                    record.PreviewStatus = (int)PreviewState.HAS_PREVIEW;
                }
                else
                {
                    record.PreviewStatus = (int)PreviewState.NO_PREVIEW;
                }
                _records.Update(record);
            }
            finally
            {
                try { if (File.Exists(tempFileSource)) File.Delete(tempFileSource); } catch { }
                try { if (File.Exists(tempFileOutput)) File.Delete(tempFileOutput); } catch { }
            }
        }

        public void Dispose()
        {
            _imageConverter?.Dispose();
        }
    }
}
