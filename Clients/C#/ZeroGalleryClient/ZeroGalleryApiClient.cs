using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ZeroGalleryClient
{
    /// <summary>
    /// Клиент для работы с ZeroGallery API
    /// </summary>
    public class ZeroGalleryApiClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string? _uploadToken;
        private readonly JsonSerializerOptions _jsonOptions;

        private const string UPLOAD_TOKEN_HEADER = "X-ZERO-UPLOAD-TOKEN";
        private const string ACCESS_TOKEN_HEADER = "X-ZERO-ACCESS-TOKEN";

        public ZeroGalleryApiClient(HttpClient client, string? uploadToken = null)
        {
            _uploadToken = uploadToken;

            _httpClient = client;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        /// <summary>
        /// Добавляет заголовки аутентификации к запросу
        /// </summary>
        private void AddAuthHeaders(HttpRequestMessage request, bool requireUploadToken = false, string accessToken = null!)
        {
            if (requireUploadToken && !string.IsNullOrWhiteSpace(_uploadToken))
            {
                request.Headers.Add(UPLOAD_TOKEN_HEADER, _uploadToken);
            }
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Add(ACCESS_TOKEN_HEADER, accessToken);
            }
        }

        /// <summary>
        /// Получает версию API
        /// </summary>
        public async Task<string> GetVersionAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/version");
            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Получает список всех альбомов
        /// </summary>
        public async Task<AlbumInfo[]> GetAlbumsAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/albums");
            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<AlbumInfo[]>(json, _jsonOptions) ?? Array.Empty<AlbumInfo>();
        }

        /// <summary>
        /// Получает элементы данных без альбома
        /// </summary>
        public async Task<DataInfo[]> GetNoAlbumDataItemsAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/data");
            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<DataInfo[]>(json, _jsonOptions) ?? Array.Empty<DataInfo>();
        }

        /// <summary>
        /// Получает элементы данных альбома
        /// </summary>
        /// <param name="albumId">ID альбома</param>
        public async Task<DataInfo[]> GetAlbumDataItemsAsync(long albumId, string accessToken = null!)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/album/{albumId}/data");
            AddAuthHeaders(request, accessToken: accessToken);

            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<DataInfo[]>(json, _jsonOptions) ?? Array.Empty<DataInfo>();
        }

        /// <summary>
        /// Получает превью изображения
        /// </summary>
        /// <param name="id">ID элемента данных</param>
        public async Task<byte[]> GetPreviewImageAsync(long id, string accessToken = null!)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/preview/{id}");
            AddAuthHeaders(request, accessToken: accessToken);

            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync();
        }

        /// <summary>
        /// Получает данные (файл)
        /// </summary>
        /// <param name="id">ID элемента данных</param>
        /// <param name="range">Диапазон байтов для загрузки (опционально, для видео)</param>
        public async Task<Stream> GetDataAsync(long id, string accessToken = null!, (long start, long end)? range = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/data/{id}");
            AddAuthHeaders(request, accessToken: accessToken);

            if (range.HasValue)
            {
                request.Headers.Range = new RangeHeaderValue(range.Value.start, range.Value.end);
            }

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStreamAsync();
        }

        /// <summary>
        /// Создает новый альбом
        /// </summary>
        /// <param name="name">Название альбома</param>
        /// <param name="description">Описание альбома</param>
        /// <param name="token">Токен доступа к альбому</param>
        /// <param name="allowRemoveData">Разрешить удаление данных</param>
        public async Task<AlbumInfo> CreateAlbumAsync(string name, string? description = null,
            string? token = null, bool allowRemoveData = false)
        {
            var albumData = new CreateAlbumInfo
            {
                Name = name,
                Description = description ?? string.Empty,
                Token = token ?? string.Empty,
                AllowRemoveData = allowRemoveData
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/album");
            AddAuthHeaders(request, requireUploadToken: true);

            var json = JsonSerializer.Serialize(albumData);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<AlbumInfo>(responseJson, _jsonOptions)!;
        }

        /// <summary>
        /// Загружает один файл
        /// </summary>
        /// <param name="filePath">Путь к файлу</param>
        /// <param name="albumId">ID альбома (опционально)</param>
        public async Task<long> UploadFileAsync(string filePath, string accessToken = null!, long? albumId = null)
        {
            using var fileStream = File.OpenRead(filePath);
            var fileName = Path.GetFileName(filePath);
            return await UploadFileAsync(fileStream, fileName, accessToken, albumId);
        }

        /// <summary>
        /// Загружает один файл из потока
        /// </summary>
        /// <param name="fileStream">Поток с данными файла</param>
        /// <param name="fileName">Имя файла</param>
        /// <param name="albumId">ID альбома (опционально)</param>
        public async Task<long> UploadFileAsync(Stream fileStream, string fileName, string accessToken = null!, long? albumId = null)
        {
            var url = albumId.HasValue ? $"/api/upload/{albumId}" : "/api/upload";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            AddAuthHeaders(request, requireUploadToken: true, accessToken: accessToken);

            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
            {
                Name = "file",
                FileName = fileName
            };

            content.Add(streamContent);
            request.Content = content;

            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<long>(json, _jsonOptions);
        }

        /// <summary>
        /// Загружает несколько файлов
        /// </summary>
        /// <param name="filePaths">Пути к файлам</param>
        /// <param name="albumId">ID альбома (опционально)</param>
        public async Task<long[]> UploadFilesAsync(string[] filePaths, string accessToken = null!, long? albumId = null)
        {
            var url = albumId.HasValue ? $"/api/upload/{albumId}" : "/api/upload";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            AddAuthHeaders(request, requireUploadToken: true, accessToken: accessToken);

            using var content = new MultipartFormDataContent();

            foreach (var filePath in filePaths)
            {
                var fileStream = File.OpenRead(filePath);
                var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                {
                    Name = "file",
                    FileName = Path.GetFileName(filePath)
                };
                content.Add(streamContent);
            }

            request.Content = content;

            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<long[]>(json, _jsonOptions)!;
        }

        /// <summary>
        /// Удаляет элемент данных
        /// </summary>
        /// <param name="id">ID элемента данных</param>
        public async Task DeleteDataAsync(long id, string accessToken = null!)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/data/{id}");
            AddAuthHeaders(request, requireUploadToken: true, accessToken: accessToken);

            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Удаляет альбом
        /// </summary>
        /// <param name="id">ID альбома</param>
        public async Task DeleteAlbumAsync(long id, string accessToken = null!)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/album/{id}");
            AddAuthHeaders(request, requireUploadToken: true, accessToken: accessToken);

            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteAll(string masterToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/all");
            request.Headers.Add(UPLOAD_TOKEN_HEADER, masterToken);

            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
