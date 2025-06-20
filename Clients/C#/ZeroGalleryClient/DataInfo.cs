namespace ZeroGalleryClient
{
    public class DataInfo
    {
        public long Id { get; set; }
        public long AlbumId { get; set; }
        public long Size { get; set; }
        public long CreatedTimestamp { get; set; }
        public string Name { get; set; }
        public string Extension { get; set; }
        public string Description { get; set; }
        public string MimeType { get; set; }
        public string Tags { get; set; }
    }
}
