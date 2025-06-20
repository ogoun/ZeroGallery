package zerogallery

import (
	"bytes"
	"encoding/json"
	"fmt"
	"io"
	"mime/multipart"
	"net/http"
	"os"
	"path/filepath"
	"strconv"
	"time"
)

// Модели данных

// AlbumInfo представляет информацию об альбоме
type AlbumInfo struct {
	ID              int64  `json:"id"`
	ImagePreviewID  int64  `json:"imagePreviewId"`
	Name            string `json:"name"`
	Description     string `json:"description"`
	IsProtected     bool   `json:"isProtected"`
}

// DataInfo представляет информацию о файле в хранилище
type DataInfo struct {
	ID                int64  `json:"id"`
	AlbumID           int64  `json:"albumId"`
	Size              int64  `json:"size"`
	CreatedTimestamp  int64  `json:"createdTimestamp"`
	Name              string `json:"name"`
	Extension         string `json:"extension"`
	Description       string `json:"description"`
	MimeType          string `json:"mimeType"`
	Tags              string `json:"tags"`
}

// CreateAlbumInfo представляет данные для создания альбома
type CreateAlbumInfo struct {
	Name            string `json:"name"`
	Description     string `json:"description"`
	Token           string `json:"token"`
	AllowRemoveData bool   `json:"allowRemoveData"`
}

// Client представляет клиент для работы с ZeroGallery API
type Client struct {
	baseURL     string
	accessToken string
	httpClient  *http.Client
}

// ProgressFunc тип функции для отслеживания прогресса загрузки
type ProgressFunc func(current, total int64)

// progressReader обертка для отслеживания прогресса чтения
type progressReader struct {
	reader   io.Reader
	total    int64
	current  int64
	callback ProgressFunc
}

func (pr *progressReader) Read(p []byte) (int, error) {
	n, err := pr.reader.Read(p)
	pr.current += int64(n)
	if pr.callback != nil {
		pr.callback(pr.current, pr.total)
	}
	return n, err
}

// NewClient создает новый клиент для работы с API
func NewClient(baseURL string, accessToken string) *Client {
	return &Client{
		baseURL:     baseURL,
		accessToken: accessToken,
		httpClient: &http.Client{
			Timeout: 30 * time.Second,
		},
	}
}

// SetHTTPClient устанавливает пользовательский HTTP клиент
func (c *Client) SetHTTPClient(client *http.Client) {
	c.httpClient = client
}

// SetAccessToken устанавливает токен доступа
func (c *Client) SetAccessToken(token string) {
	c.accessToken = token
}

// doRequest выполняет HTTP запрос
func (c *Client) doRequest(method, endpoint string, body io.Reader) (*http.Response, error) {
	url := c.baseURL + "/" + endpoint
	req, err := http.NewRequest(method, url, body)
	if err != nil {
		return nil, err
	}

	if c.accessToken != "" {
		req.Header.Set("X-Access-Token", c.accessToken)
	}

	if body != nil && method != "GET" {
		req.Header.Set("Content-Type", "application/json")
	}

	resp, err := c.httpClient.Do(req)
	if err != nil {
		return nil, err
	}

	if resp.StatusCode == http.StatusUnauthorized {
		resp.Body.Close()
		return nil, fmt.Errorf("unauthorized access")
	}

	if resp.StatusCode == http.StatusNotFound {
		resp.Body.Close()
		return nil, fmt.Errorf("resource not found: %s", endpoint)
	}

	if resp.StatusCode >= 400 {
		bodyBytes, _ := io.ReadAll(resp.Body)
		resp.Body.Close()
		return nil, fmt.Errorf("API error: status=%d, body=%s", resp.StatusCode, string(bodyBytes))
	}

	return resp, nil
}

// GetVersion получает версию API
func (c *Client) GetVersion() (string, error) {
	resp, err := c.doRequest("GET", "api/version", nil)
	if err != nil {
		return "", err
	}
	defer resp.Body.Close()

	body, err := io.ReadAll(resp.Body)
	if err != nil {
		return "", err
	}

	return string(body), nil
}

// GetAlbums получает список альбомов
func (c *Client) GetAlbums() ([]AlbumInfo, error) {
	resp, err := c.doRequest("GET", "api/albums", nil)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	var albums []AlbumInfo
	if err := json.NewDecoder(resp.Body).Decode(&albums); err != nil {
		return nil, err
	}

	return albums, nil
}

// CreateAlbum создает новый альбом
func (c *Client) CreateAlbum(info CreateAlbumInfo) (*AlbumInfo, error) {
	body, err := json.Marshal(info)
	if err != nil {
		return nil, err
	}

	resp, err := c.doRequest("POST", "api/album", bytes.NewReader(body))
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	var album AlbumInfo
	if err := json.NewDecoder(resp.Body).Decode(&album); err != nil {
		return nil, err
	}

	return &album, nil
}

// DeleteAlbum удаляет альбом
func (c *Client) DeleteAlbum(albumID int64) error {
	endpoint := fmt.Sprintf("api/album/%d", albumID)
	resp, err := c.doRequest("DELETE", endpoint, nil)
	if err != nil {
		return err
	}
	resp.Body.Close()
	return nil
}

// GetDataWithoutAlbums получает данные без привязки к альбомам
func (c *Client) GetDataWithoutAlbums() ([]DataInfo, error) {
	resp, err := c.doRequest("GET", "api/data", nil)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	var data []DataInfo
	if err := json.NewDecoder(resp.Body).Decode(&data); err != nil {
		return nil, err
	}

	return data, nil
}

// GetAlbumData получает данные альбома
func (c *Client) GetAlbumData(albumID int64) ([]DataInfo, error) {
	endpoint := fmt.Sprintf("api/album/%d/data", albumID)
	resp, err := c.doRequest("GET", endpoint, nil)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	var data []DataInfo
	if err := json.NewDecoder(resp.Body).Decode(&data); err != nil {
		return nil, err
	}

	return data, nil
}

// UploadFile загружает файл
func (c *Client) UploadFile(filePath string, albumID int64) (int64, error) {
	file, err := os.Open(filePath)
	if err != nil {
		return 0, err
	}
	defer file.Close()

	return c.UploadFileReader(file, filepath.Base(filePath), albumID)
}

// UploadFileReader загружает файл из io.Reader
func (c *Client) UploadFileReader(reader io.Reader, filename string, albumID int64) (int64, error) {
	body := &bytes.Buffer{}
	writer := multipart.NewWriter(body)

	part, err := writer.CreateFormFile("file", filename)
	if err != nil {
		return 0, err
	}

	if _, err := io.Copy(part, reader); err != nil {
		return 0, err
	}

	if err := writer.Close(); err != nil {
		return 0, err
	}

	endpoint := "api/upload"
	if albumID > 0 {
		endpoint = fmt.Sprintf("api/upload/%d", albumID)
	}

	req, err := http.NewRequest("POST", c.baseURL+"/"+endpoint, body)
	if err != nil {
		return 0, err
	}

	if c.accessToken != "" {
		req.Header.Set("X-Access-Token", c.accessToken)
	}
	req.Header.Set("Content-Type", writer.FormDataContentType())

	resp, err := c.httpClient.Do(req)
	if err != nil {
		return 0, err
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		bodyBytes, _ := io.ReadAll(resp.Body)
		return 0, fmt.Errorf("upload failed: status=%d, body=%s", resp.StatusCode, string(bodyBytes))
	}

	var fileID int64
	if err := json.NewDecoder(resp.Body).Decode(&fileID); err != nil {
		return 0, err
	}

	return fileID, nil
}

// UploadMultipleFiles загружает несколько файлов
func (c *Client) UploadMultipleFiles(filePaths []string, albumID int64) ([]int64, error) {
	body := &bytes.Buffer{}
	writer := multipart.NewWriter(body)

	for _, filePath := range filePaths {
		file, err := os.Open(filePath)
		if err != nil {
			return nil, err
		}
		defer file.Close()

		part, err := writer.CreateFormFile("files", filepath.Base(filePath))
		if err != nil {
			return nil, err
		}

		if _, err := io.Copy(part, file); err != nil {
			return nil, err
		}
	}

	if err := writer.Close(); err != nil {
		return nil, err
	}

	endpoint := "api/upload"
	if albumID > 0 {
		endpoint = fmt.Sprintf("api/upload/%d", albumID)
	}

	req, err := http.NewRequest("POST", c.baseURL+"/"+endpoint, body)
	if err != nil {
		return nil, err
	}

	if c.accessToken != "" {
		req.Header.Set("X-Access-Token", c.accessToken)
	}
	req.Header.Set("Content-Type", writer.FormDataContentType())

	resp, err := c.httpClient.Do(req)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		bodyBytes, _ := io.ReadAll(resp.Body)
		return nil, fmt.Errorf("upload failed: status=%d, body=%s", resp.StatusCode, string(bodyBytes))
	}

	var fileIDs []int64
	if err := json.NewDecoder(resp.Body).Decode(&fileIDs); err != nil {
		return nil, err
	}

	return fileIDs, nil
}

// GetPreview получает превью изображения
func (c *Client) GetPreview(dataID int64) (io.ReadCloser, error) {
	endpoint := fmt.Sprintf("api/preview/%d", dataID)
	resp, err := c.doRequest("GET", endpoint, nil)
	if err != nil {
		return nil, err
	}

	return resp.Body, nil
}

// SavePreview сохраняет превью в файл
func (c *Client) SavePreview(dataID int64, outputPath string) error {
	reader, err := c.GetPreview(dataID)
	if err != nil {
		return err
	}
	defer reader.Close()

	file, err := os.Create(outputPath)
	if err != nil {
		return err
	}
	defer file.Close()

	_, err = io.Copy(file, reader)
	return err
}

// GetData получает данные файла
func (c *Client) GetData(dataID int64) (io.ReadCloser, error) {
	endpoint := fmt.Sprintf("api/data/%d", dataID)
	resp, err := c.doRequest("GET", endpoint, nil)
	if err != nil {
		return nil, err
	}

	return resp.Body, nil
}

// DownloadData скачивает файл с отслеживанием прогресса
func (c *Client) DownloadData(dataID int64, outputPath string, progress ProgressFunc) error {
	endpoint := fmt.Sprintf("api/data/%d", dataID)
	resp, err := c.doRequest("GET", endpoint, nil)
	if err != nil {
		return err
	}
	defer resp.Body.Close()

	file, err := os.Create(outputPath)
	if err != nil {
		return err
	}
	defer file.Close()

	if progress != nil {
		contentLength := resp.ContentLength
		reader := &progressReader{
			reader:   resp.Body,
			total:    contentLength,
			callback: progress,
		}
		_, err = io.Copy(file, reader)
	} else {
		_, err = io.Copy(file, resp.Body)
	}

	return err
}

// VideoStreamOptions опции для получения видео потока
type VideoStreamOptions struct {
	RangeStart *int64
	RangeEnd   *int64
}

// GetVideoStream получает видео поток с поддержкой Range запросов
func (c *Client) GetVideoStream(dataID int64, opts *VideoStreamOptions) (io.ReadCloser, map[string]string, error) {
	endpoint := fmt.Sprintf("api/data/%d", dataID)
	req, err := http.NewRequest("GET", c.baseURL+"/"+endpoint, nil)
	if err != nil {
		return nil, nil, err
	}

	if c.accessToken != "" {
		req.Header.Set("X-Access-Token", c.accessToken)
	}

	if opts != nil {
		if opts.RangeStart != nil && opts.RangeEnd != nil {
			req.Header.Set("Range", fmt.Sprintf("bytes=%d-%d", *opts.RangeStart, *opts.RangeEnd))
		} else if opts.RangeStart != nil {
			req.Header.Set("Range", fmt.Sprintf("bytes=%d-", *opts.RangeStart))
		} else if opts.RangeEnd != nil {
			req.Header.Set("Range", fmt.Sprintf("bytes=-%d", *opts.RangeEnd))
		}
	}

	resp, err := c.httpClient.Do(req)
	if err != nil {
		return nil, nil, err
	}

	if resp.StatusCode != http.StatusOK && resp.StatusCode != http.StatusPartialContent {
		resp.Body.Close()
		return nil, nil, fmt.Errorf("failed to get video stream: status=%d", resp.StatusCode)
	}

	headers := make(map[string]string)
	headers["Content-Range"] = resp.Header.Get("Content-Range")
	headers["Content-Length"] = resp.Header.Get("Content-Length")
	headers["Content-Type"] = resp.Header.Get("Content-Type")

	return resp.Body, headers, nil
}

// DeleteData удаляет файл
func (c *Client) DeleteData(dataID int64) error {
	endpoint := fmt.Sprintf("api/data/%d", dataID)
	resp, err := c.doRequest("DELETE", endpoint, nil)
	if err != nil {
		return err
	}
	resp.Body.Close()
	return nil
}

// Вспомогательные методы

// GetCreatedTime возвращает время создания DataInfo как time.Time
func (d *DataInfo) GetCreatedTime() time.Time {
	return time.Unix(d.CreatedTimestamp/1000, (d.CreatedTimestamp%1000)*1000000)
}

// FormatSize форматирует размер файла в человекочитаемый вид
func (d *DataInfo) FormatSize() string {
	const unit = 1024
	if d.Size < unit {
		return fmt.Sprintf("%d B", d.Size)
	}
	div, exp := int64(unit), 0
	for n := d.Size / unit; n >= unit; n /= unit {
		div *= unit
		exp++
	}
	return fmt.Sprintf("%.1f %cB", float64(d.Size)/float64(div), "KMGTPE"[exp])
}