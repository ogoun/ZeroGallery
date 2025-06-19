using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using ZeroLevel;

namespace VideoStreamingTestApp.Controllers
{
    [ApiController]
    [Route("api/video")]
    public class VideoController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;

        public VideoController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        [HttpGet("stream/{fileName}")]
        public async Task<IActionResult> StreamVideo(string fileName)
        {
            // Безопасность: проверяем имя файла
            if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains(".."))
                return BadRequest("Invalid file name");

            var filePath = Path.Combine(Configuration.BaseDirectory, "Videos", fileName);

            if (!System.IO.File.Exists(filePath))
                return NotFound("Video file not found");

            var fileInfo = new FileInfo(filePath);
            var contentType = GetContentType(filePath);

            // Поддержка Range requests для больших файлов
            if (Request.Headers.ContainsKey("Range"))
            {
                return await GetPartialContent(filePath, contentType, fileInfo.Length);
            }

            // Полный файл
            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            return File(stream, contentType, enableRangeProcessing: true);
        }

        private async Task<IActionResult> GetPartialContent(string filePath, string contentType, long fileLength)
        {
            var rangeHeader = Request.Headers["Range"].ToString();
            var range = ParseRange(rangeHeader, fileLength);

            if (!range.HasValue)
                return BadRequest("Invalid range");

            var (start, end) = range.Value;
            var contentLength = end - start + 1;

            Response.StatusCode = 206; // Partial Content
            Response.Headers.Add("Content-Range", $"bytes {start}-{end}/{fileLength}");
            Response.Headers.Add("Accept-Ranges", "bytes");
            Response.Headers.Add("Content-Length", contentLength.ToString());
            Response.ContentType = contentType;

            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            stream.Seek(start, SeekOrigin.Begin);

            var buffer = new byte[4096];
            var bytesRemaining = contentLength;

            while (bytesRemaining > 0)
            {
                var bytesToRead = (int)Math.Min(buffer.Length, bytesRemaining);
                var bytesRead = await stream.ReadAsync(buffer, 0, bytesToRead);

                if (bytesRead == 0)
                    break;

                await Response.Body.WriteAsync(buffer, 0, bytesRead);
                bytesRemaining -= bytesRead;
            }

            stream.Dispose();
            return new EmptyResult();
        }

        private (long start, long end)? ParseRange(string rangeHeader, long fileLength)
        {
            if (!rangeHeader.StartsWith("bytes="))
                return null;

            var range = rangeHeader.Substring(6);
            var parts = range.Split('-');

            if (parts.Length != 2)
                return null;

            long start = 0;
            long end = fileLength - 1;

            if (!string.IsNullOrEmpty(parts[0]))
            {
                if (!long.TryParse(parts[0], out start))
                    return null;
            }

            if (!string.IsNullOrEmpty(parts[1]))
            {
                if (!long.TryParse(parts[1], out end))
                    return null;
            }
            else
            {
                end = fileLength - 1;
            }

            if (start > end || start < 0 || end >= fileLength)
                return null;

            return (start, end);
        }

        private string GetContentType(string filePath)
        {
            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(filePath, out var contentType))
            {
                contentType = "application/octet-stream";
            }
            return contentType;
        }
    }
}
