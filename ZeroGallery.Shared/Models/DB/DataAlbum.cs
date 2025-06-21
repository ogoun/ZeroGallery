using SQLite;

namespace ZeroGallery.Shared.Models.DB
{
    /// <summary>
    /// Альбом
    /// </summary>
    public class DataAlbum
        : IEquatable<DataAlbum>
    {
        [PrimaryKey, AutoIncrement]
        public long Id { get; set; } = -1;

        /// <summary>
        /// Изображение на обложку альбома
        /// </summary>
        public long ImagePreviewId { get; set; } = -1;

        /// <summary>
        /// Название альбома
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Описание альбома
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Токен доступа к данным группы
        /// </summary>
        public string Token { get; set; } = default!;

        /// <summary>
        /// Разрешение на удаление данных (и соотвественно самого альбома) с токеном доступа к альбома
        /// Мастер токен позволяет удалить альбом и данные даже если флаг задан как false
        /// </summary>
        public bool AllowRemoveData { get; set; }

        /// <summary>
        /// Альбом находится в процессе удаления
        /// </summary>
        [Indexed]
        public bool InRemoving { get; set; }

        public override bool Equals(object? obj)
        {
            return this.Equals(obj as DataAlbum);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public bool Equals(DataAlbum? other)
        {
            if(other == null) return false;
            if(ReferenceEquals(this, other)) return true;
            if(Id  != other.Id) return false;
            if(ImagePreviewId != other.ImagePreviewId) return false;
            if(Name.IsEqual(other.Name) == false) return false;
            if(Description.IsEqual(other.Description) == false) return false;
            if(Token.IsEqual(other.Token) == false) return false;
            return true;
        }
    }
}
