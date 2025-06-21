using ZeroGallery.Shared.Models;
using ZeroGallery.Shared.Models.DB;

namespace ZeroGallery.Shared.Services.DB
{
    public static class DataMapper
    {
        public static DataInfo Map(DataRecord entity)
        {
            var data = new DataInfo
            {
                AlbumId = entity.AlbumId,
                CreatedTimestamp = entity.CreatedTimestamp,
                Description = entity.Description,
                Extension = entity.Extension,
                Id = entity.Id,
                MimeType = entity.MimeType,
                Name = entity.Name,
                Size = entity.Size,
                Tags = entity.Tags,
                HasPreview = PreviewState.HAS_PREVIEW == (PreviewState)entity.PreviewStatus,
            };
            return data;
        }
    }
}
