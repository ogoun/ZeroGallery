using ImageMagick;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using System.Buffers;
using ZeroLevel;

namespace ZeroGallery.Shared.Services
{
    public interface IImageConverter
        : IDisposable
    {
        Task<byte[]> ConvertToJpgAsync(Stream inputStream, string inputFormat,
            int quality = 85, CancellationToken cancellationToken = default);
        bool IsFormatSupported(string format);
    }

    public class UnifiedImageConverter : IImageConverter
    {
        private readonly Dictionary<string, Func<Stream, int, CancellationToken, Task<byte[]>>> _converters;
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed;

        public UnifiedImageConverter()
        {
            _semaphore = new SemaphoreSlim(Environment.ProcessorCount);

            _converters = new Dictionary<string, Func<Stream, int, CancellationToken, Task<byte[]>>>(
                StringComparer.OrdinalIgnoreCase)
            {
                // Standard formats handled by ImageSharp
                ["png"] = ConvertWithImageSharp,
                ["bmp"] = ConvertWithImageSharp,
                ["gif"] = ConvertWithImageSharp,
                ["tiff"] = ConvertWithImageSharp,
                ["tif"] = ConvertWithImageSharp,
                ["webp"] = ConvertWithImageSharp,
                ["jpg"] = ConvertWithImageSharp,
                ["jpeg"] = ConvertWithImageSharp,

                // Complex formats requiring specialized libraries
                ["heic"] = ConvertHeicWithMagick,
                ["heif"] = ConvertHeicWithMagick,
                ["svg"] = ConvertSvgWithSkia,
                ["ico"] = ConvertIcoWithMagick,

                // RAW formats - all use ImageMagick for conversion
                ["dng"] = ConvertRawWithMagick,
                ["cr2"] = ConvertRawWithMagick,  // Canon RAW 2
                ["nef"] = ConvertRawWithMagick,  // Nikon Electronic Format
                ["arw"] = ConvertRawWithMagick,  // Sony Alpha RAW
                ["orf"] = ConvertRawWithMagick,  // Olympus RAW Format
            };
        }

        public async Task<byte[]> ConvertToJpgAsync(Stream inputStream, string inputFormat,
            int quality = 85, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(inputStream);

            if (string.IsNullOrWhiteSpace(inputFormat))
                throw new ArgumentException("Input format cannot be empty", nameof(inputFormat));

            var normalizedFormat = inputFormat.ToLowerInvariant().TrimStart('.');

            if (!_converters.TryGetValue(normalizedFormat, out var converter))
            {
                throw new NotSupportedException(
                    $"Format '{inputFormat}' is not supported. Supported formats: {string.Join(", ", _converters.Keys)}");
            }

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var result = await converter(inputStream, quality, cancellationToken);

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to convert {InputFormat} to JPG", normalizedFormat);
                throw new Exception(
                    $"Failed to convert {normalizedFormat} to JPG: {ex.Message}", ex);
            }
            finally
            {
                _semaphore.Release();
            }
        }


        #region JPEG
        private readonly MagickReadSettings _readSettings = new MagickReadSettings
        {
            Format = MagickFormat.Svg,
            Density = new Density(300, 300), // Высокое разрешение для качественной растеризации
            BackgroundColor = MagickColors.White, // Белый фон для JPG
            ColorSpace = ColorSpace.sRGB
        };

        private async Task<byte[]> ConvertWithImageSharp(Stream inputStream, int quality,
            CancellationToken cancellationToken)
        {
            using var image = await Image.LoadAsync(inputStream, cancellationToken);
            using var outputStream = new MemoryStream();

            var encoder = new JpegEncoder
            {
                Quality = quality,
                //Subsample = JpegSubsample.Ratio420,
                //ColorType = JpegColorType.YCbCrRatio420
            };

            await image.SaveAsync(outputStream, encoder, cancellationToken);
            return outputStream.ToArray();
        }

        private async Task<byte[]> ConvertHeicWithMagick(Stream inputStream, int quality,
            CancellationToken cancellationToken)
        {
            // Use ArrayPool for efficient memory management
            var buffer = ArrayPool<byte>.Shared.Rent((int)inputStream.Length);
            try
            {
                var bytesRead = await inputStream.ReadAsync(buffer.AsMemory(0, (int)inputStream.Length),
                    cancellationToken);

                using var image = new MagickImage(buffer, 0, (uint)bytesRead);
                image.Format = MagickFormat.Jpeg;
                image.Quality = (uint)quality;

                return image.ToByteArray();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private async Task<byte[]> ConvertSvgWithSkia(Stream inputStream, int quality,
            CancellationToken cancellationToken)
        {
            // Читаем SVG в память для работы с Magick.NET
            using var memoryStream = new MemoryStream();
            await inputStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            using var image = new MagickImage(memoryStream, _readSettings);
            // Ограничиваем максимальный размер, сохраняя пропорции
            const int maxDimension = 2048;
            if (image.Width > maxDimension || image.Height > maxDimension)
            {
                var geometry = new MagickGeometry(maxDimension, maxDimension)
                {
                    IgnoreAspectRatio = false,
                    Greater = false
                };
                image.Resize(geometry);
            }
            // Настройки для конвертации в JPEG
            image.Format = MagickFormat.Jpeg;
            image.Quality = (uint)quality;
            // Убираем альфа-канал для JPEG
            image.Alpha(AlphaOption.Remove);
            // Оптимизация для web
            image.Strip();
            return image.ToByteArray();
        }

        private async Task<byte[]> ConvertIcoWithMagick(Stream inputStream, int quality,
            CancellationToken cancellationToken)
        {
            using var collection = new MagickImageCollection();
            await Task.Run(() => collection.Read(inputStream, MagickFormat.Ico), cancellationToken);

            // Select the largest icon in the collection
            var largestIcon = collection.OrderByDescending(i => i.Width * i.Height).FirstOrDefault();

            if (largestIcon == null)
                throw new InvalidOperationException("No valid icon found in ICO file");

            largestIcon.Format = MagickFormat.Jpeg;
            largestIcon.Quality = (uint)quality;

            return largestIcon.ToByteArray();
        }

        /// <summary>
        /// Универсальный метод для конвертации RAW форматов (DNG, CR2, NEF, ARW, ORF)
        /// </summary>
        private async Task<byte[]> ConvertRawWithMagick(Stream inputStream, int quality,
            CancellationToken cancellationToken)
        {
            var buffer = ArrayPool<byte>.Shared.Rent((int)inputStream.Length);
            try
            {
                var bytesRead = await inputStream.ReadAsync(buffer.AsMemory(0, (int)inputStream.Length),
                    cancellationToken);

                // Используем базовые настройки, которые работают для всех RAW форматов
                // ImageMagick автоматически определит тип RAW файла
                using var image = new MagickImage(buffer, 0, (uint)bytesRead);

                // Настройки обработки изображения
                image.Format = MagickFormat.Jpeg;
                image.Quality = (uint)quality;

                // Автоматическая коррекция уровней для лучшего отображения
                image.AutoLevel();

                // Опционально: автоматическая коррекция гаммы
                image.AutoGamma();

                // Нормализация изображения для улучшения контраста
                image.Normalize();

                // Применяем цветовой профиль sRGB для корректного отображения
                image.ColorSpace = ColorSpace.sRGB;

                // Повышение резкости для компенсации потерь при конвертации из RAW
                image.UnsharpMask(0.5, 0.5, 3, 0.05);

                // Удаляем лишние метаданные для уменьшения размера
                image.Strip();

                // Ограничиваем максимальный размер для больших RAW файлов
                const int maxDimension = 4096;
                if (image.Width > maxDimension || image.Height > maxDimension)
                {
                    var geometry = new MagickGeometry(maxDimension, maxDimension)
                    {
                        IgnoreAspectRatio = false,
                        Greater = false
                    };
                    image.Resize(geometry);
                }

                return image.ToByteArray();
            }
            catch (MagickException ex) when (ex.Message.Contains("delegate"))
            {
                // Если ImageMagick не может обработать RAW из-за отсутствия делегата,
                // пробуем альтернативный метод
                throw new NotSupportedException(
                    $"RAW format processing requires additional ImageMagick delegates. " +
                    $"Ensure dcraw or LibRaw is installed. Original error: {ex.Message}", ex);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        #endregion

        public bool IsFormatSupported(string format)
        {
            var normalizedFormat = format?.ToLowerInvariant().TrimStart('.');
            return !string.IsNullOrEmpty(normalizedFormat) && _converters.ContainsKey(normalizedFormat);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _semaphore?.Dispose();
                _disposed = true;
            }
        }
    }
}
