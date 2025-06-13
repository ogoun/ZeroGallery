using ZeroGallery.Shared.Services;

namespace ZeroGallery.Shared.Models
{
    public sealed class ImageTypeInfo
    {
        public const string DEFAULT_IMAGE_EXTENSION = ".jpg";

        public string Extension { get; set; }
        public string MimeType { get; set; }

        public ImageTypeInfo(string extension, string mimeType)
        {
            Extension = extension;
            MimeType = mimeType;
        }

        public bool IsImage() => KnownImages.IsImage(Extension);
        public bool IsVideo() => KnownVideos.IsVideo(Extension);
    }
}
