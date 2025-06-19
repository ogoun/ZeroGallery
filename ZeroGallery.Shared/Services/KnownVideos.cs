namespace ZeroGallery.Shared.Services
{
    public static class KnownVideos
    {
        private static readonly HashSet<string> _knownVideoExtensions = new HashSet<string>
        {
            ".mov",
            ".mp4",
            ".avi",
            ".webm",
            ".wmv",
            ".mkv",
        };

        public static bool IsVideo(string extension) => _knownVideoExtensions.Contains(extension.ToLowerInvariant());
    }
}
