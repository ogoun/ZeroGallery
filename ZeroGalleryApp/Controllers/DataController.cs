using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;
using ZeroGallery.Shared.Models;
using ZeroGallery.Shared.Services;
using ZeroGallery.Shared.Services.DB;
using ZeroLevel;

namespace ZeroGalleryApp.Controllers
{
    [Route("api")]
    [ApiController]
    [Produces(MediaTypeNames.Application.Json)]
    public class DataController : BaseController
    {
        private readonly DataStorage _storage;

        public DataController(AppConfig config, DataStorage storage) : base(config)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            _storage = storage;
        }

        [HttpGet("version")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        public ActionResult<string> GetVersion()
        {
            return Ok(DataStorage.VERSION);
        }

        [HttpGet("albums")]
        [ProducesResponseType(typeof(AlbumInfo[]), StatusCodes.Status200OK)]
        public ActionResult<AlbumInfo[]> GetAlbums()
        {
            try
            {
                var albums = _storage.GetAlbums();
                if (albums?.Any() ?? false)
                {
                    var result = albums.Select(a => AlbumMapper.Map(a)).ToList();
                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DataController.GetAlbums]");
            }
            return Ok(Enumerable.Empty<AlbumInfo>());
        }

        [HttpGet("data")]
        [ProducesResponseType(typeof(DataInfo[]), StatusCodes.Status200OK)]
        public ActionResult<DataInfo[]> GetNoAlbumDataItems()
        {
            try
            {
                var items = _storage.GetDataWithoutAlbums();
                if (items?.Any() ?? false)
                {
                    var result = items.Select(a => DataMapper.Map(a)).ToList();
                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DataController.GetNoAlbumDataItems]");
            }
            return Ok(Enumerable.Empty<AlbumInfo>());
        }

        [HttpGet("album/{id}/data")]
        [ProducesResponseType(typeof(DataInfo[]), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public ActionResult<DataInfo[]> GetAlbumDataItems([FromRoute] long id)
        {
            try
            {
                if (CanViewImages(_storage.GetAlbumToken(id)))
                {
                    var items = _storage.GetDataByAlbum(id);
                    if (items?.Any() ?? false)
                    {
                        var result = items.Select(a => DataMapper.Map(a)).ToList();
                        return Ok(result);
                    }
                }
                else
                {
                    Log.Warning($"[DataController.GetAlbumDataItems] Incorrect album token. AlbumId:'{id}'. Token: {OperationContext.AccessToken ?? string.Empty}");
                    return Unauthorized();
                }
            }
            catch (Exception ex)
            {
                Error(ex, $"[DataController.GetAlbumDataItems] AlbumId: {id}");
            }
            return Ok(Enumerable.Empty<DataInfo>());
        }

        [HttpGet("preview/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Produces("image/jpeg", "image/png", "image/webp", MediaTypeNames.Application.Octet)]
        public IActionResult GetPreviewImage([FromRoute] long id)
        {
            try
            {
                var info = _storage.GetPreview(id);
                if (info == null)
                {
                    Log.Warning($"[DataController.GetPreviewImage] Not found data {id}");
                    return NotFound();
                }
                if (CanViewImages(_storage.GetItemAlbumToken(id)) && System.IO.File.Exists(info.FilePath)) // файл превью может не существовать для некоторых типов файлов
                {
                    return PhysicalFile(info.FilePath, info.MimeType, info.Name);
                }
                return GetBlankFileForDataType(info.DataType);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ImageController.GetPreviewImage]");
            }
            return GetBlankFileForDataType(DataType.Binary);
        }

        [HttpGet("data/{id}")]
        [EnableCors("AllowAll")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status206PartialContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [Produces(MediaTypeNames.Application.Octet)]
        public async Task<IActionResult> GetData([FromRoute] long id)
        {
            try
            {
                if (CanViewImages(_storage.GetItemAlbumToken(id)) == false)
                {
                    return Unauthorized();
                }
                var info = _storage.GetData(id);
                if (info == null)
                {
                    Log.Warning($"[DataController.GetData] Not found data {id}");
                    return NotFound();
                }

                // Check if file exists
                if (!System.IO.File.Exists(info.FilePath))
                {
                    Log.Warning($"[DataController.GetData] File not found on disk: {info.FilePath}");
                    return NotFound();
                }

                var fileInfo = new FileInfo(info.FilePath);

                var isVideo = info.DataType == DataType.Video;

                // Support Range requests for video files
                if (isVideo && Request.Headers.ContainsKey("Range"))
                {
                    return await GetPartialContent(info.FilePath, info.MimeType, fileInfo.Length);
                }

                // For non-video files or full file requests
                var stream = new FileStream(info.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                return File(stream, info.MimeType, info.Name, enableRangeProcessing: isVideo);
            }
            catch (Exception ex)
            {
                Error(ex, $"[DataController.GetData] Id: {id}");
                return BadRequest(ex.Message);
            }
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

        [HttpPost("album")]
        [Consumes(MediaTypeNames.Application.Json)]
        [ProducesResponseType(typeof(AlbumInfo), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public ActionResult<AlbumInfo> CreateAlbum([FromBody] CreateAlbumInfo info)
        {
            if (string.IsNullOrWhiteSpace(info.Name))
            {
                return BadRequest("Empty album name");
            }
            try
            {
                if (CanCreateAlbum())
                {
                    var album = _storage.AppendAlbum(info.Name, info.Description, info.Token, info.AllowRemoveData);
                    Log.Info($"[DataController.CreateAlbum] Album '{info.Name}' created");
                    return Ok(AlbumMapper.Map(album));
                }
                return Unauthorized();
            }
            catch (Exception ex)
            {
                Error(ex, $"[DataController.CreateAlbum] Fault create album '{info.Name}'. Token: '{info.Token ?? string.Empty}'. Description: {info.Description ?? string.Empty}");
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("upload/{albumId?}")]
        [DisableRequestSizeLimit]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(long), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(long[]), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Upload([FromRoute] long albumId = -1)
        {
            try
            {
                if (CanUploadImages(_storage.GetAlbumToken(albumId)) == false)
                {
                    return Unauthorized();
                }

                var files = Request?.Form?.Files;
                if (files == null || files.Count == 0)
                {
                    return BadRequest("No files for upload");
                }
                if (files.Count == 1)
                {
                    var file = files[0];
                    Log.Debug($"[DataController.Upload] Receive file to upload.");
                    var name = file.FileName;
                    var record = await _storage.WriteData(name, string.Empty, string.Empty, albumId, file.OpenReadStream());
                    return Ok(record.Id);
                }
                else
                {
                    var ids = new long[files.Count];
                    int idx_index = 0;
                    Log.Debug($"[DataController.Upload] Receive {files.Count} files to upload.");
                    foreach (var file in files)
                    {
                        var name = file.FileName;
                        var record = await _storage.WriteData(name, string.Empty, string.Empty, albumId, file.OpenReadStream());
                        ids[idx_index++] = record.Id;
                    }
                    return Ok(ids);
                }
            }
            catch (Exception ex)
            {
                Error(ex, $"[DataController.Upload] Fault upload images to album '{albumId}'");
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("data/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult DeleteData([FromRoute] long id)
        {
            if (CanRemoveItem(_storage.GetItemAlbumToken(id)))
            {
                _storage.RemoveRecord(id, HasAdminAccess());
                return Ok();
            }
            return Unauthorized();
        }

        [HttpDelete("album/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult DeleteAlbum([FromRoute] long id)
        {
            if (CanRemoveAlbum(_storage.GetAlbumToken(id)))
            {
                _storage.RemoveAlbum(id, HasAdminAccess());
                return Ok();
            }
            return Unauthorized();
        }

        private PhysicalFileResult GetBlankFileForDataType(DataType dataType)
        {
            switch (dataType)
            {
                case DataType.Image:
                    return PhysicalFile(@"./assets/binary.jpg", "image/jpeg", "image.jpg");
                case DataType.Video:
                    return PhysicalFile(@"./assets/video.jpg", "image/jpeg", "image.jpg");
            }
            return PhysicalFile(@"./assets/binary.jpg", "image/jpeg", "image.jpg");
        }
    }
}