using SQLite;

namespace ZeroGallery.Shared.Models.DB
{
    /// <summary>
    /// Запись о данных в хранилище
    /// </summary>
    public class DataRecord
        : IEquatable<DataRecord>
    {
        [PrimaryKey, AutoIncrement]
        public long Id { get; set; } = -1;

        /// <summary>
        /// Группа доступа
        /// </summary>
        [Indexed]
        public long AlbumId { get; set; } = -1;

        /// <summary>
        /// Размер файла
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Отметка времени, когда файл был добавлен
        /// </summary>
        public long CreatedTimestamp { get; set; }

        /// <summary>
        /// Шард индекс (подкаталог)
        /// </summary>
        public int ShardIndex { get; set; }

        /// <summary>
        /// Идекс в шарде (имя файла)
        /// </summary>
        public int Index { get; set; }

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

        /// <summary>
        /// Указывает что запись находится на удалении
        /// </summary>
        [Indexed]
        public bool InRemoving { get; set; }

        /// <summary>
        /// Статус создания превью
        /// </summary>
        [Indexed]
        public int PreviewStatus { get; set; }

        /// <summary>
        /// Статус конвертации
        /// </summary>
        [Indexed]
        public int ConvertStatus { get; set; }

        public override bool Equals(object? obj)
        {
            return this.Equals(obj as DataRecord);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public bool Equals(DataRecord? other)
        {
            if (other == null) return false;
            if (ReferenceEquals(null, other)) return true;
            if (Id != other.Id) return false;
            if (AlbumId != other.AlbumId) return false;
            if (Size != other.Size) return false;
            if (CreatedTimestamp != other.CreatedTimestamp) return false;
            if (ShardIndex != other.ShardIndex) return false;
            if (Index != other.Index) return false;
            if (Name.IsEqual(other.Name) == false) return false;
            if (Extension.IsEqual(other.Extension) == false) return false;
            if (Description.IsEqual(other.Description) == false) return false;
            if (MimeType.IsEqual(other.MimeType) == false) return false;
            if (Tags.IsEqual(other.Tags) == false) return false;
            return true;
        }
    }
}
