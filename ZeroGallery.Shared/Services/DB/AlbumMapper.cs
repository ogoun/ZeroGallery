using ZeroGallery.Shared.Models;
using ZeroGallery.Shared.Models.DB;

namespace ZeroGallery.Shared.Services.DB
{
    public static class AlbumMapper
    {
        public static AlbumInfo Map(DataAlbum entity)
        {
            var info = new AlbumInfo();
            info.Id = entity.Id;
            info.Name = entity.Name;
            info.ImagePreviewId = entity.ImagePreviewId;
            info.Description = entity.Description;
            info.IsProtected = string.IsNullOrEmpty(entity.Token) == false;
            return info;
        }
    }
}
