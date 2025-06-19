using FFMpegCore;
using ZeroLevel;

namespace ZeroGallery.Shared.Services
{
    public static class VideoThumbnailService
    {
        private static readonly Random _random = new Random((int)Environment.TickCount);
        public static async Task<bool> GenerateThumbnailAsync(
            string videoPath, string outputPath,
            ThumbnailOptions options = null!)
        {
            options ??= ThumbnailOptions.Default;

            try
            {
                var mediaInfo = await FFProbe.AnalyseAsync(videoPath);
                var position = GetSmartRandomPosition(mediaInfo.Duration);
                return await ExtractOptimizedFrameAsync(videoPath, outputPath, position, options);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to generate thumbnail for {Video}", videoPath);
                throw;
            }
        }

        private static TimeSpan GetSmartRandomPosition(TimeSpan duration)
        {
            var minSeconds = duration.TotalSeconds * 0.05;
            var maxSeconds = duration.TotalSeconds * 0.95;
            return TimeSpan.FromSeconds(_random.NextDouble() * (maxSeconds - minSeconds) + minSeconds);
        }

        private static Task<bool> ExtractOptimizedFrameAsync(
            string inputPath,
            string outputPath,
            TimeSpan position,
            ThumbnailOptions options)
        {
            return FFMpegArguments
                .FromFileInput(inputPath, false, inputOptions => inputOptions.Seek(position))
                .OutputToFile(outputPath, true, outputOptions =>
                {
                    outputOptions
                        .WithFrameOutputCount(1)
                        .WithVideoFilters(filterOptions =>
                        {
                            filterOptions.Scale(options.Width, options.Height);
                        });
                    outputOptions.WithCustomArgument($"-q:v {options.JpegQuality}");
                })
                .ProcessAsynchronously();
        }
    }

    public class ThumbnailOptions
    {
        public int Width { get; set; } = 480;
        public int Height { get; set; } = -1; // -1 = автоматически
        public TimeSpan? Position { get; set; }
        public int JpegQuality { get; set; } = 2; // 1-31
        public static ThumbnailOptions Default => new();
        public static ThumbnailOptions HighQuality => new()
        {
            Width = 1280,
            Height = 720,
            JpegQuality = 1,
        };
    }
}
