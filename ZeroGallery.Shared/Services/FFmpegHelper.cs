using FFMpegCore;
using System.Runtime.InteropServices;

namespace ZeroGallery.Shared.Services
{
    public static class FFmpegHelper
    {
        public static void ConfigureFFmpeg()
        {
            var ffmpegPath = GetFFmpegPath();
            GlobalFFOptions.Configure(new FFOptions
            {
                BinaryFolder = ffmpegPath,
                TemporaryFilesFolder = Path.GetTempPath()
            });
        }

        private static string GetFFmpegPath()
        {
            string rid = null!;
            string ffmpeg_name = null!;
            string ffprobe_name = null!;
            // Для development окружения
            if (string.IsNullOrEmpty(rid))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    rid = "windows-64";
                    ffmpeg_name = "ffmpeg.exe";
                    ffprobe_name = "ffprobe.exe";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    rid = "linux-64";
                    ffmpeg_name = "ffmpeg";
                    ffprobe_name = "ffprobe";
                }
            }

            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var ffmpegPath = Path.Combine(basePath, "runtimes", rid);

            if (!Directory.Exists(ffmpegPath))
                throw new DirectoryNotFoundException($"FFmpeg binaries not found: {ffmpegPath}");

            // FFMPEG
            var ffmpeg_file = Path.Combine(ffmpegPath, ffmpeg_name);
            if (!File.Exists(ffmpeg_file))
            {
                var zipFile =  Directory.GetFiles(ffmpegPath, "ffmpeg*.*").FirstOrDefault();
                if (zipFile != null)
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipFile, ffmpegPath);
                }
            }

            //FFPROBE
            var ffprobe_file = Path.Combine(ffmpegPath, ffprobe_name);
            if (!File.Exists(ffprobe_file))
            {
                var zipFile = Directory.GetFiles(ffmpegPath, "ffprobe*.*").FirstOrDefault();
                if (zipFile != null)
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipFile, ffmpegPath);
                }
            }
            return ffmpegPath;
        }
    }
}
