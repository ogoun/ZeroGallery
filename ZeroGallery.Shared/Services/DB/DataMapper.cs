using ZeroGallery.Shared.Models;
using ZeroGallery.Shared.Models.DB;

namespace ZeroGallery.Shared.Services.DB
{
    public static class DataMapper
    {
        public static DataInfo Map(DataRecord entity)
        {
            var data = new DataInfo();
            data.AlbumId = entity.AlbumId;
            data.CreatedTimestamp = entity.CreatedTimestamp;
            data.Description = entity.Description;
            data.Extension = entity.Extension;
            data.Id = entity.Id;
            data.MimeType = entity.MimeType;
            data.Name = entity.Name;
            data.Size = entity.Size;
            data.Tags = entity.Tags;
            return data;
        }
    }
}
