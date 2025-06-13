namespace ZeroGallery.Shared.Models
{
    /// <summary>
    /// Альбом
    /// </summary>
    public sealed class AlbumInfo
    {
        public long Id { get; set; } 

        /// <summary>
        /// Изображение на обложку альбома
        /// </summary>
        public long ImagePreviewId { get; set; }

        /// <summary>
        /// Название альбома
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Описание альбома
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Альбом защищен токеном доступа
        /// </summary>
        public bool IsProtected { get; set; }
    }
}
