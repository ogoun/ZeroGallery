using FFMpegCore;
using FFMpegCore.Enums;

namespace ZeroGallery.Shared.Services
{
    public static class VideoConverter
    {
        public static async Task<bool> ConvertToMp4Async(
            string inputPath,
            string outputPath,
            VideoQuality quality = VideoQuality.High,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var mediaInfo = await FFProbe.AnalyseAsync(inputPath);
                var conversion = FFMpegArguments
                    .FromFileInput(inputPath)
                    .OutputToFile(outputPath, true, options => options
                        .WithVideoCodec(VideoCodec.LibX264)
                        .WithConstantRateFactor(GetCrfValue(quality))
                        .WithAudioCodec(AudioCodec.Aac)
                        .WithAudioBitrate(128)
                        .WithFastStart() // Оптимизация для веб-воспроизведения
                        .WithCustomArgument("-movflags +faststart"));
                var result = await conversion.ProcessAsynchronously(true);
                return result;
            }
            catch (OperationCanceledException)
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
                throw;
            }
        }

        private static int GetCrfValue(VideoQuality quality) => quality switch
        {
            VideoQuality.Ultra => 18,
            VideoQuality.High => 21,
            VideoQuality.Medium => 23,
            VideoQuality.Low => 28,
            _ => 23
        };
    }

    public enum VideoQuality
    {
        Low, Medium, High, Ultra
    }
}
