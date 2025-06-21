using ImageMagick;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SkiaSharp;
using Svg.Skia;
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
                ["ico"] = ConvertIcoWithMagick
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
            using var svg = new SKSvg();

            // Load SVG asynchronously
            await Task.Run(() => svg.Load(inputStream), cancellationToken);

            if (svg.Picture == null)
                throw new InvalidOperationException("Failed to load SVG");

            // Calculate output dimensions maintaining aspect ratio
            var bounds = svg.Picture.CullRect;
            var targetWidth = Math.Min(2048, bounds.Width);
            var scale = targetWidth / bounds.Width;
            var targetHeight = bounds.Height * scale;

            using var bitmap = new SKBitmap((int)targetWidth, (int)targetHeight);
            using var canvas = new SKCanvas(bitmap);

            canvas.Clear(SKColors.White); // White background for JPG
            canvas.Scale(scale);
            canvas.DrawPicture(svg.Picture);

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);

            return data.ToArray();
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
