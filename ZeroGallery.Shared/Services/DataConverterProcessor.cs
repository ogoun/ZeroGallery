using ZeroGallery.Shared.Models.DB;
using ZeroGallery.Shared.Services.DB;
using ZeroGalleryApp;
using ZeroLevel;
using ZeroLevel.Services.FileSystem;

namespace ZeroGallery.Shared.Services
{
    public sealed class DataConverterProcessor
        : IDisposable
    {
        private readonly IImageConverter _imageConverter;
        private readonly DataRecordRepository _records;
        private readonly DataStorage _storage;
        private readonly AppConfig _config;

        public DataConverterProcessor(AppConfig config,
            DataRecordRepository recordsRepository,
            DataStorage storage)
        {
            _config = config;
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
            foreach (var record in _records.GetWaitingConvertRecords())
            {
                try
                {
                    if (KnownImages.IsImage(record.Extension))
                    {
                        if (_config.convert_heic_to_jpg || _config.convert_tiff_to_jpg)
                        {
                            await HandleConvertImage(record);
                        }
                        else
                        {
                            record.ConvertStatus = (int)ConvertDataState.COMPLETED;
                            _records.Update(record);
                        }
                    }
                    else if (KnownVideos.IsVideo(record.Extension))
                    {
                        if (_config.convert_video_to_mp4)
                        {
                            await HandleConvertVideo(record);
                        }
                        else
                        {
                            record.ConvertStatus = (int)ConvertDataState.COMPLETED;
                            _records.Update(record);
                        }
                    }
                    else
                    {
                        // При правильном процессе сюда вообще не должны попадать
                        record.ConvertStatus = (int)ConvertDataState.COMPLETED;
                        _records.Update(record);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"[DataConverterProcessor.Collect] Fault proceed record '{record.Id}'");
                }
            }
        }

        private async Task HandleConvertImage(DataRecord record)
        {
            byte[] converted;
            switch (record.Extension)
            {
                case ".heic":
                    if (_config.convert_heic_to_jpg)
                    {
                        var data = _storage.GetData(record);
                        var thumbFilePath = _storage.GetPreviewPath(record);

                        using (var dataStream = File.OpenRead(data.FilePath))
                        {
                            converted = await _imageConverter.ConvertToJpgAsync(dataStream, record.Extension);
                        }

                        File.Delete(data.FilePath);
                        File.WriteAllBytes(data.FilePath, converted);

                        record.ConvertStatus = (int)ConvertDataState.COMPLETED;
                        record.Extension = ".jpg";
                        record.MimeType = "image/jpeg";

                        _records.Update(record);

                        Log.Info($"[DataConverterProcessor.HandleConvertImage] HEIC file converted to JPG. Record '{record.Id}'");
                    }
                    else
                    {
                        record.ConvertStatus = (int)ConvertDataState.COMPLETED;
                        _records.Update(record);
                    }
                    break;
                case ".tiff":
                    if (_config.convert_tiff_to_jpg)
                    {
                        var data = _storage.GetData(record);
                        var thumbFilePath = _storage.GetPreviewPath(record);

                        using (var dataStream = File.OpenRead(data.FilePath))
                        {
                            converted = await _imageConverter.ConvertToJpgAsync(dataStream, record.Extension);
                        }

                        File.Delete(data.FilePath);
                        File.WriteAllBytes(data.FilePath, converted);

                        record.ConvertStatus = (int)ConvertDataState.COMPLETED;
                        record.Extension = ".jpg";
                        record.MimeType = "image/jpeg";

                        _records.Update(record);
                        Log.Info($"[DataConverterProcessor.HandleConvertImage] TIFF file converted to JPG. Record '{record.Id}'");
                    }
                    else
                    {
                        record.ConvertStatus = (int)ConvertDataState.COMPLETED;
                        _records.Update(record);
                    }
                    break;
                default:
                    record.ConvertStatus = (int)ConvertDataState.COMPLETED;
                    _records.Update(record);
                    break;
            }
        }

        private async Task HandleConvertVideo(DataRecord record)
        {
            if (record.Extension.IsEqual(".mp4"))
            {
                record.ConvertStatus = (int)ConvertDataState.COMPLETED;
                _records.Update(record);
            }
            else
            {
                var output = FSUtils.GetAppLocalTemporaryFile() + ".mp4";
                if (File.Exists(output))
                {
                    File.Delete(output);
                }
                var ext = record.Extension;
                var data = _storage.GetData(record);
                var succsessfully_convert = await VideoConverter.ConvertToMp4Async(data.FilePath, output);
                if (succsessfully_convert)
                {
                    File.Move(output, data.FilePath, true);
                    record.Extension = ".mp4";
                    record.MimeType = "video/mp4";
                    Log.Info($"[DataConverterProcessor.HandleConvertVideo] {ext} file converted to MP4. Record '{record.Id}'");
                }
                else
                {
                    Log.Warning($"[DataConverterProcessor.HandleConvertVideo] Can't convert video file to .mp4 for record '{record.Id}'");
                }
                record.ConvertStatus = (int)ConvertDataState.COMPLETED;
                _records.Update(record);
            }
        }

        public void Dispose()
        {
            _imageConverter?.Dispose();
        }
    }
}
