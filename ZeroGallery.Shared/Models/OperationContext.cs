namespace ZeroGallery.Shared.Models
{
    /// <summary>
    /// Контекст исполнения операций
    /// </summary>
    public sealed class OperationContext(long startOperationTimestamp)
    {
        /// <summary>
        /// Временная отметка начала операции
        /// </summary>
        public long StartOperationTimestamp { get; } = startOperationTimestamp;
        /// <summary>
        /// Токен доступа к альбому
        /// </summary>
        public string? AccessToken { get; private set; }
        /// <summary>
        /// Токен для записи данных
        /// </summary>
        public string? UploadToken { get; private set; }

        public void SetTokens(string? accessToken, string? uploadToken)
        {
            AccessToken = accessToken;
            UploadToken = uploadToken;
        }
    }
}
