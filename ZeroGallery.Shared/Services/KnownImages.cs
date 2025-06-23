namespace ZeroGallery.Shared.Services
{
    public static class KnownImages
    {
        private static readonly HashSet<string> _knownImageExtensions = new HashSet<string>
        {
            ".png",
            ".jpg",
            ".bmp",
            ".gif",
            ".heic",
            ".ico",
            ".svg",
            ".tiff",
            ".webp",

            // RAW
            ".dng",
            ".cr2",
            ".nef",
            ".arw",
            ".orf",
        };
        public static bool IsImage(string extension) => _knownImageExtensions.Contains(extension.ToLowerInvariant());
    }
}
