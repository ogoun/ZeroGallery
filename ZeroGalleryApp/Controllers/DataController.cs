using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using ZeroGallery.Shared;
using ZeroGallery.Shared.Models;
using ZeroGallery.Shared.Services;
using ZeroGallery.Shared.Services.DB;
using ZeroLevel;

namespace ZeroGalleryApp.Controllers
{
    [Route("api")]
    [ApiController]
    public class DataController : BaseController
    {
        private readonly DataStorage _storage;
        private readonly string _uploadToken;
        public DataController(AppConfig config, DataStorage storage) : base()
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));

            _storage = storage;
            _uploadToken = config.api_write_token ?? string.Empty;
        }

        [HttpGet("albums")]
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
        public ActionResult<DataInfo[]> GetAlbumDataItems(long id)
        {
            try
            {
                if (_storage.HasAccessToAlbum(id, OperationContext.AccessToken!))
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
                Log.Error(ex, "[DataController.GetAlbumDataItems]");
            }
            return Ok(Enumerable.Empty<DataInfo>());
        }

        [HttpPost("album")]
        public ActionResult<AlbumInfo> CreateAlbum([FromBody] CreateAlbumInfo info)
        {
            if (OperationContext.UploadToken!.IsEqual(_uploadToken) == false)
            {
                Log.Warning($"[DataController.CreateAlbum] Wrong upload token '{OperationContext.UploadToken}'");
                return Unauthorized();
            }
            var album = _storage.AppendAlbum(info.Name, info.Description, info.Token);
            return Ok(AlbumMapper.Map(album));
        }

        [HttpPost("upload/{albumId?}")]
        public async Task<IActionResult> Upload(long albumId = -1)
        {
            if (OperationContext.UploadToken!.IsEqual(_uploadToken) == false)
            {
                Log.Warning($"[DataController.Upload] Wrong upload token '{OperationContext.UploadToken}'");
                return Unauthorized();
            }

            var album_id = -1L;
            if (albumId != -1)
            {
                if (_storage.HasAccessToAlbum(albumId, OperationContext.AccessToken!))
                {
                    album_id = albumId;
                }
                else
                {
                    Log.Warning($"[DataController.Upload] Wrong album({albumId}) access token '{OperationContext.AccessToken}'");
                    return Unauthorized();
                }
            }

            try
            {
                var files = Request?.Form?.Files;
                if (files != null && files.Count == 1)
                {
                    var file = files[0];
                    Log.Debug($"[DataController.Upload] Receive file to upload.");
                    if (file.Length > 0)
                    {
                        var name = file.FileName;
                        var record = await _storage.WriteData(name, string.Empty, string.Empty, album_id, file.OpenReadStream());
                        return Ok(record.Id);
                    }
                }
                else
                {
                    var ids = new long[files.Count];
                    int idx_index = 0;
                    Log.Debug($"[DataController.Upload] Receive {files.Count} files to upload.");
                    foreach (var file in files)
                    {
                        if (file.Length > 0)
                        {
                            var name = file.FileName;
                            var record = await _storage.WriteData(name, string.Empty, string.Empty, album_id, file.OpenReadStream());
                            ids[idx_index++] = record.Id;
                        }
                    }
                    return Ok(ids);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DataController.Upload]");
            }
            return BadRequest();
        }

        [HttpGet("preview/{id}")]
        public IActionResult GetPreviewImage(long id)
        {
            try
            {
                var info = _storage.GetPreview(id);
                if (info == null)
                {
                    Log.Warning($"[DataController.GetPreviewImage] Not found data {id}");
                    return NotFound();
                }

                if (_storage.HasAccessToItem(id, OperationContext.AccessToken))
                {
                    if (string.IsNullOrEmpty(info.FilePath))
                    {
                        return GetBlankFileForDataType(info.DataType);
                    }
                    return PhysicalFile(info.FilePath, info.MimeType, info.Name);
                }
                else
                {
                    return GetBlankFileForDataType(info.DataType);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ImageController.GetPreviewImage]");
            }
            return GetBlankFileForDataType(DataType.Binary);
        }

        [HttpGet("data/{id}")]
        [EnableCors("AllowAll")]
        public IActionResult GetData(long id)
        {
            try
            {
                var info = _storage.GetData(id);
                if (info == null)
                {
                    Log.Warning($"[DataController.GetData] Not found data {id}");
                    return NotFound();
                }

                if (_storage.HasAccessToItem(id, OperationContext.AccessToken))
                {
                    return PhysicalFile(info.FilePath, info.MimeType, info.Name);
                }
                else
                {
                    Log.Warning($"[DataController.GetData] Unauthorized access to data {id} with token '{OperationContext.AccessToken}' or data not found");
                    return Unauthorized();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DataController.GetData]");
                return BadRequest();
            }
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
