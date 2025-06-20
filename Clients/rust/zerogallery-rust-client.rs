use reqwest::{
    header::{HeaderMap, HeaderValue, CONTENT_TYPE, RANGE},
    multipart::{Form, Part},
    Body, Client, Response, StatusCode,
};
use serde::{Deserialize, Serialize};
use std::path::Path;
use std::time::{Duration, SystemTime, UNIX_EPOCH};
use tokio::fs::File;
use tokio::io::{AsyncReadExt, AsyncWriteExt};
use tokio_util::codec::{BytesCodec, FramedRead};

pub type Result<T> = std::result::Result<T, Error>;

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("HTTP request failed: {0}")]
    Request(#[from] reqwest::Error),
    
    #[error("IO error: {0}")]
    Io(#[from] std::io::Error),
    
    #[error("Unauthorized access")]
    Unauthorized,
    
    #[error("Resource not found: {0}")]
    NotFound(String),
    
    #[error("API error: status={status}, message={message}")]
    Api { status: u16, message: String },
    
    #[error("Invalid response format")]
    InvalidResponse,
}

// Модели данных

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AlbumInfo {
    pub id: i64,
    pub image_preview_id: i64,
    pub name: String,
    pub description: String,
    pub is_protected: bool,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DataInfo {
    pub id: i64,
    pub album_id: i64,
    pub size: i64,
    pub created_timestamp: i64,
    pub name: String,
    pub extension: String,
    pub description: String,
    pub mime_type: String,
    pub tags: String,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateAlbumInfo {
    pub name: String,
    pub description: String,
    pub token: String,
    pub allow_remove_data: bool,
}

impl DataInfo {
    /// Получить время создания как SystemTime
    pub fn created_time(&self) -> SystemTime {
        let secs = self.created_timestamp / 1000;
        let nanos = ((self.created_timestamp % 1000) * 1_000_000) as u32;
        UNIX_EPOCH + Duration::new(secs as u64, nanos)
    }
    
    /// Форматировать размер файла в человекочитаемый вид
    pub fn format_size(&self) -> String {
        const UNITS: &[&str] = &["B", "KB", "MB", "GB", "TB", "PB"];
        const UNIT_SIZE: f64 = 1024.0;
        
        if self.size == 0 {
            return "0 B".to_string();
        }
        
        let size = self.size as f64;
        let exp = (size.ln() / UNIT_SIZE.ln()).floor() as usize;
        let exp = exp.min(UNITS.len() - 1);
        let size = size / UNIT_SIZE.powi(exp as i32);
        
        if exp == 0 {
            format!("{} {}", self.size, UNITS[0])
        } else {
            format!("{:.1} {}", size, UNITS[exp])
        }
    }
}

/// Callback для отслеживания прогресса
pub type ProgressCallback = Box<dyn Fn(u64, u64) + Send + Sync>;

/// Клиент для работы с ZeroGallery API
pub struct ZeroGalleryClient {
    client: Client,
    base_url: String,
    access_token: Option<String>,
}

impl ZeroGalleryClient {
    /// Создать новый клиент
    pub fn new(base_url: impl Into<String>) -> Self {
        Self::with_token(base_url, None)
    }
    
    /// Создать клиент с токеном доступа
    pub fn with_token(base_url: impl Into<String>, access_token: Option<String>) -> Self {
        let client = Client::builder()
            .timeout(Duration::from_secs(30))
            .build()
            .expect("Failed to create HTTP client");
            
        Self {
            client,
            base_url: base_url.into().trim_end_matches('/').to_string(),
            access_token,
        }
    }
    
    /// Установить токен доступа
    pub fn set_access_token(&mut self, token: Option<String>) {
        self.access_token = token;
    }
    
    /// Создать заголовки с токеном
    fn create_headers(&self) -> HeaderMap {
        let mut headers = HeaderMap::new();
        if let Some(token) = &self.access_token {
            headers.insert("X-Access-Token", HeaderValue::from_str(token).unwrap());
        }
        headers
    }
    
    /// Обработать ответ API
    async fn handle_response<T: for<'de> Deserialize<'de>>(
        &self,
        response: Response,
    ) -> Result<T> {
        match response.status() {
            StatusCode::OK => {
                response.json::<T>().await.map_err(|_| Error::InvalidResponse)
            }
            StatusCode::UNAUTHORIZED => Err(Error::Unauthorized),
            StatusCode::NOT_FOUND => Err(Error::NotFound("Resource not found".to_string())),
            status => {
                let message = response.text().await.unwrap_or_default();
                Err(Error::Api {
                    status: status.as_u16(),
                    message,
                })
            }
        }
    }
    
    /// Получить версию API
    pub async fn get_version(&self) -> Result<String> {
        let url = format!("{}/api/version", self.base_url);
        let response = self.client
            .get(&url)
            .headers(self.create_headers())
            .send()
            .await?;
            
        match response.status() {
            StatusCode::OK => Ok(response.text().await?),
            StatusCode::UNAUTHORIZED => Err(Error::Unauthorized),
            status => {
                let message = response.text().await.unwrap_or_default();
                Err(Error::Api {
                    status: status.as_u16(),
                    message,
                })
            }
        }
    }
    
    /// Получить список альбомов
    pub async fn get_albums(&self) -> Result<Vec<AlbumInfo>> {
        let url = format!("{}/api/albums", self.base_url);
        let response = self.client
            .get(&url)
            .headers(self.create_headers())
            .send()
            .await?;
            
        self.handle_response(response).await
    }
    
    /// Создать новый альбом
    pub async fn create_album(&self, info: CreateAlbumInfo) -> Result<AlbumInfo> {
        let url = format!("{}/api/album", self.base_url);
        let mut headers = self.create_headers();
        headers.insert(CONTENT_TYPE, HeaderValue::from_static("application/json"));
        
        let response = self.client
            .post(&url)
            .headers(headers)
            .json(&info)
            .send()
            .await?;
            
        self.handle_response(response).await
    }
    
    /// Удалить альбом
    pub async fn delete_album(&self, album_id: i64) -> Result<()> {
        let url = format!("{}/api/album/{}", self.base_url, album_id);
        let response = self.client
            .delete(&url)
            .headers(self.create_headers())
            .send()
            .await?;
            
        match response.status() {
            StatusCode::OK => Ok(()),
            StatusCode::UNAUTHORIZED => Err(Error::Unauthorized),
            status => {
                let message = response.text().await.unwrap_or_default();
                Err(Error::Api {
                    status: status.as_u16(),
                    message,
                })
            }
        }
    }
    
    /// Получить данные без альбомов
    pub async fn get_data_without_albums(&self) -> Result<Vec<DataInfo>> {
        let url = format!("{}/api/data", self.base_url);
        let response = self.client
            .get(&url)
            .headers(self.create_headers())
            .send()
            .await?;
            
        self.handle_response(response).await
    }
    
    /// Получить данные альбома
    pub async fn get_album_data(&self, album_id: i64) -> Result<Vec<DataInfo>> {
        let url = format!("{}/api/album/{}/data", self.base_url, album_id);
        let response = self.client
            .get(&url)
            .headers(self.create_headers())
            .send()
            .await?;
            
        self.handle_response(response).await
    }
    
    /// Загрузить файл
    pub async fn upload_file<P: AsRef<Path>>(
        &self,
        file_path: P,
        album_id: i64,
    ) -> Result<i64> {
        let file_path = file_path.as_ref();
        let file_name = file_path
            .file_name()
            .and_then(|n| n.to_str())
            .ok_or_else(|| Error::Io(std::io::Error::new(
                std::io::ErrorKind::InvalidInput,
                "Invalid file name",
            )))?;
            
        let mut file = File::open(file_path).await?;
        let mut contents = Vec::new();
        file.read_to_end(&mut contents).await?;
        
        self.upload_file_data(&contents, file_name, album_id).await
    }
    
    /// Загрузить файл из данных
    pub async fn upload_file_data(
        &self,
        data: &[u8],
        filename: &str,
        album_id: i64,
    ) -> Result<i64> {
        let url = if album_id > 0 {
            format!("{}/api/upload/{}", self.base_url, album_id)
        } else {
            format!("{}/api/upload", self.base_url)
        };
        
        let part = Part::bytes(data.to_vec())
            .file_name(filename.to_string());
            
        let form = Form::new().part("file", part);
        
        let response = self.client
            .post(&url)
            .headers(self.create_headers())
            .multipart(form)
            .send()
            .await?;
            
        self.handle_response(response).await
    }
    
    /// Загрузить несколько файлов
    pub async fn upload_multiple_files<P: AsRef<Path>>(
        &self,
        file_paths: &[P],
        album_id: i64,
    ) -> Result<Vec<i64>> {
        let url = if album_id > 0 {
            format!("{}/api/upload/{}", self.base_url, album_id)
        } else {
            format!("{}/api/upload", self.base_url)
        };
        
        let mut form = Form::new();
        
        for file_path in file_paths {
            let file_path = file_path.as_ref();
            let file_name = file_path
                .file_name()
                .and_then(|n| n.to_str())
                .ok_or_else(|| Error::Io(std::io::Error::new(
                    std::io::ErrorKind::InvalidInput,
                    "Invalid file name",
                )))?;
                
            let mut file = File::open(file_path).await?;
            let mut contents = Vec::new();
            file.read_to_end(&mut contents).await?;
            
            let part = Part::bytes(contents)
                .file_name(file_name.to_string());
            form = form.part("files", part);
        }
        
        let response = self.client
            .post(&url)
            .headers(self.create_headers())
            .multipart(form)
            .send()
            .await?;
            
        self.handle_response(response).await
    }
    
    /// Получить превью
    pub async fn get_preview(&self, data_id: i64) -> Result<Vec<u8>> {
        let url = format!("{}/api/preview/{}", self.base_url, data_id);
        let response = self.client
            .get(&url)
            .headers(self.create_headers())
            .send()
            .await?;
            
        match response.status() {
            StatusCode::OK => Ok(response.bytes().await?.to_vec()),
            StatusCode::UNAUTHORIZED => Err(Error::Unauthorized),
            StatusCode::NOT_FOUND => Err(Error::NotFound(format!("Preview for data {} not found", data_id))),
            status => {
                let message = response.text().await.unwrap_or_default();
                Err(Error::Api {
                    status: status.as_u16(),
                    message,
                })
            }
        }
    }
    
    /// Сохранить превью в файл
    pub async fn save_preview<P: AsRef<Path>>(
        &self,
        data_id: i64,
        output_path: P,
    ) -> Result<()> {
        let data = self.get_preview(data_id).await?;
        let mut file = File::create(output_path).await?;
        file.write_all(&data).await?;
        Ok(())
    }
    
    /// Получить данные файла
    pub async fn get_data(&self, data_id: i64) -> Result<Vec<u8>> {
        let url = format!("{}/api/data/{}", self.base_url, data_id);
        let response = self.client
            .get(&url)
            .headers(self.create_headers())
            .send()
            .await?;
            
        match response.status() {
            StatusCode::OK => Ok(response.bytes().await?.to_vec()),
            StatusCode::UNAUTHORIZED => Err(Error::Unauthorized),
            StatusCode::NOT_FOUND => Err(Error::NotFound(format!("Data {} not found", data_id))),
            status => {
                let message = response.text().await.unwrap_or_default();
                Err(Error::Api {
                    status: status.as_u16(),
                    message,
                })
            }
        }
    }
    
    /// Скачать файл с прогрессом
    pub async fn download_data<P: AsRef<Path>>(
        &self,
        data_id: i64,
        output_path: P,
        progress: Option<ProgressCallback>,
    ) -> Result<()> {
        let url = format!("{}/api/data/{}", self.base_url, data_id);
        let response = self.client
            .get(&url)
            .headers(self.create_headers())
            .send()
            .await?;
            
        match response.status() {
            StatusCode::OK => {
                let total_size = response
                    .headers()
                    .get("content-length")
                    .and_then(|ct_len| ct_len.to_str().ok())
                    .and_then(|ct_len| ct_len.parse::<u64>().ok())
                    .unwrap_or(0);
                    
                let mut file = File::create(output_path).await?;
                let mut downloaded = 0u64;
                let mut stream = response.bytes_stream();
                
                use futures_util::StreamExt;
                while let Some(chunk) = stream.next().await {
                    let chunk = chunk?;
                    file.write_all(&chunk).await?;
                    downloaded += chunk.len() as u64;
                    
                    if let Some(ref callback) = progress {
                        callback(downloaded, total_size);
                    }
                }
                
                Ok(())
            }
            StatusCode::UNAUTHORIZED => Err(Error::Unauthorized),
            StatusCode::NOT_FOUND => Err(Error::NotFound(format!("Data {} not found", data_id))),
            status => {
                let message = response.text().await.unwrap_or_default();
                Err(Error::Api {
                    status: status.as_u16(),
                    message,
                })
            }
        }
    }
    
    /// Получить видео поток с поддержкой Range
    pub async fn get_video_stream(
        &self,
        data_id: i64,
        range_start: Option<u64>,
        range_end: Option<u64>,
    ) -> Result<(Vec<u8>, VideoHeaders)> {
        let url = format!("{}/api/data/{}", self.base_url, data_id);
        let mut headers = self.create_headers();
        
        if range_start.is_some() || range_end.is_some() {
            let range_value = match (range_start, range_end) {
                (Some(start), Some(end)) => format!("bytes={}-{}", start, end),
                (Some(start), None) => format!("bytes={}-", start),
                (None, Some(end)) => format!("bytes=-{}", end),
                _ => unreachable!(),
            };
            headers.insert(RANGE, HeaderValue::from_str(&range_value).unwrap());
        }
        
        let response = self.client
            .get(&url)
            .headers(headers)
            .send()
            .await?;
            
        let status = response.status();
        if status != StatusCode::OK && status != StatusCode::PARTIAL_CONTENT {
            let message = response.text().await.unwrap_or_default();
            return Err(Error::Api {
                status: status.as_u16(),
                message,
            });
        }
        
        let video_headers = VideoHeaders {
            content_range: response
                .headers()
                .get("content-range")
                .and_then(|h| h.to_str().ok())
                .map(|s| s.to_string()),
            content_length: response
                .headers()
                .get("content-length")
                .and_then(|h| h.to_str().ok())
                .map(|s| s.to_string()),
            content_type: response
                .headers()
                .get("content-type")
                .and_then(|h| h.to_str().ok())
                .map(|s| s.to_string()),
        };
        
        let data = response.bytes().await?.to_vec();
        Ok((data, video_headers))
    }
    
    /// Удалить файл
    pub async fn delete_data(&self, data_id: i64) -> Result<()> {
        let url = format!("{}/api/data/{}", self.base_url, data_id);
        let response = self.client
            .delete(&url)
            .headers(self.create_headers())
            .send()
            .await?;
            
        match response.status() {
            StatusCode::OK => Ok(()),
            StatusCode::UNAUTHORIZED => Err(Error::Unauthorized),
            status => {
                let message = response.text().await.unwrap_or_default();
                Err(Error::Api {
                    status: status.as_u16(),
                    message,
                })
            }
        }
    }
}

/// Заголовки видео ответа
#[derive(Debug, Clone)]
pub struct VideoHeaders {
    pub content_range: Option<String>,
    pub content_length: Option<String>,
    pub content_type: Option<String>,
}

#[cfg(test)]
mod tests {
    use super::*;
    
    #[test]
    fn test_format_size() {
        let test_cases = vec![
            (0, "0 B"),
            (100, "100 B"),
            (1024, "1.0 KB"),
            (1536, "1.5 KB"),
            (1048576, "1.0 MB"),
            (1073741824, "1.0 GB"),
        ];
        
        for (size, expected) in test_cases {
            let data = DataInfo {
                id: 1,
                album_id: 1,
                size,
                created_timestamp: 0,
                name: String::new(),
                extension: String::new(),
                description: String::new(),
                mime_type: String::new(),
                tags: String::new(),
            };
            
            assert_eq!(data.format_size(), expected);
        }
    }
    
    #[test]
    fn test_created_time() {
        let data = DataInfo {
            id: 1,
            album_id: 1,
            size: 0,
            created_timestamp: 1640995200000, // 2022-01-01 00:00:00 UTC
            name: String::new(),
            extension: String::new(),
            description: String::new(),
            mime_type: String::new(),
            tags: String::new(),
        };
        
        let time = data.created_time();
        let duration = time.duration_since(UNIX_EPOCH).unwrap();
        assert_eq!(duration.as_secs(), 1640995200);
    }
}