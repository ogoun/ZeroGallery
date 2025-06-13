namespace ZeroGallery.Shared.Models
{
    /// <summary>
    /// Запись о данных в хранилище
    /// </summary>
    public class DataInfo
    {
        public long Id { get; set; }

        /// <summary>
        /// Группа доступа
        /// </summary>
        public long AlbumId { get; set; }

        /// <summary>
        /// Размер файла
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Отметка времени, когда файл был добавлен
        /// </summary>
        public long CreatedTimestamp { get; set; }

        /// <summary>
        /// Название файла
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Расширение файла
        /// </summary>
        public string Extension { get; set; }

        /// <summary>
        /// Описание файла
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Тип содержимого
        /// </summary>
        public string MimeType { get; set; }

        /// <summary>
        /// Теги через ;
        /// </summary>
        public string Tags { get; set; }
    }
}
