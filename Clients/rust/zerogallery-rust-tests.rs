// tests/integration_tests.rs
use mockito::{mock, Mock, Server};
use zerogallery::{AlbumInfo, CreateAlbumInfo, DataInfo, ZeroGalleryClient};

fn create_test_client(server_url: &str) -> ZeroGalleryClient {
    ZeroGalleryClient::with_token(server_url, Some("test-token".to_string()))
}

#[tokio::test]
async fn test_get_version() {
    let mut server = Server::new();
    let url = server.url();
    
    let _m = server
        .mock("GET", "/api/version")
        .match_header("X-Access-Token", "test-token")
        .with_status(200)
        .with_body("1.0.0")
        .create();
    
    let client = create_test_client(&url);
    let version = client.get_version().await.unwrap();
    assert_eq!(version, "1.0.0");
}

#[tokio::test]
async fn test_get_albums() {
    let mut server = Server::new();
    let url = server.url();
    
    let albums_json = r#"[
        {
            "id": 1,
            "imagePreviewId": 0,
            "name": "Album 1",
            "description": "Test album 1",
            "isProtected": false
        },
        {
            "id": 2,
            "imagePreviewId": 0,
            "name": "Album 2",
            "description": "Test album 2",
            "isProtected": true
        }
    ]"#;
    
    let _m = server
        .mock("GET", "/api/albums")
        .with_status(200)
        .with_header("content-type", "application/json")
        .with_body(albums_json)
        .create();
    
    let client = create_test_client(&url);
    let albums = client.get_albums().await.unwrap();
    
    assert_eq!(albums.len(), 2);
    assert_eq!(albums[0].name, "Album 1");
    assert!(!albums[0].is_protected);
    assert_eq!(albums[1].name, "Album 2");
    assert!(albums[1].is_protected);
}

#[tokio::test]
async fn test_create_album() {
    let mut server = Server::new();
    let url = server.url();
    
    let response_json = r#"{
        "id": 3,
        "imagePreviewId": 0,
        "name": "New Album",
        "description": "Test description",
        "isProtected": true
    }"#;
    
    let _m = server
        .mock("POST", "/api/album")
        .match_header("content-type", "application/json")
        .with_status(200)
        .with_header("content-type", "application/json")
        .with_body(response_json)
        .create();
    
    let client = create_test_client(&url);
    let album = client
        .create_album(CreateAlbumInfo {
            name: "New Album".to_string(),
            description: "Test description".to_string(),
            token: "album-token".to_string(),
            allow_remove_data: false,
        })
        .await
        .unwrap();
    
    assert_eq!(album.id, 3);
    assert_eq!(album.name, "New Album");
    assert!(album.is_protected);
}

#[tokio::test]
async fn test_upload_file_data() {
    let mut server = Server::new();
    let url = server.url();
    
    let _m = server
        .mock("POST", "/api/upload/1")
        .match_header("X-Access-Token", "test-token")
        .with_status(200)
        .with_body("123")
        .create();
    
    let client = create_test_client(&url);
    let file_id = client
        .upload_file_data(b"test content", "test.txt", 1)
        .await
        .unwrap();
    
    assert_eq!(file_id, 123);
}

#[tokio::test]
async fn test_get_album_data() {
    let mut server = Server::new();
    let url = server.url();
    
    let data_json = r#"[
        {
            "id": 1,
            "albumId": 1,
            "size": 1024,
            "createdTimestamp": 1640995200000,
            "name": "file1.txt",
            "extension": "txt",
            "description": "",
            "mimeType": "text/plain",
            "tags": ""
        }
    ]"#;
    
    let _m = server
        .mock("GET", "/api/album/1/data")
        .with_status(200)
        .with_header("content-type", "application/json")
        .with_body(data_json)
        .create();
    
    let client = create_test_client(&url);
    let data = client.get_album_data(1).await.unwrap();
    
    assert_eq!(data.len(), 1);
    assert_eq!(data[0].name, "file1.txt");
    assert_eq!(data[0].size, 1024);
}

#[tokio::test]
async fn test_download_data() {
    let mut server = Server::new();
    let url = server.url();
    
    let _m = server
        .mock("GET", "/api/data/1")
        .with_status(200)
        .with_header("content-length", "12")
        .with_body("test content")
        .create();
    
    let client = create_test_client(&url);
    
    let temp_dir = tempfile::tempdir().unwrap();
    let output_path = temp_dir.path().join("downloaded.txt");
    
    client.download_data(1, &output_path, None).await.unwrap();
    
    let content = tokio::fs::read_to_string(&output_path).await.unwrap();
    assert_eq!(content, "test content");
}

#[tokio::test]
async fn test_download_with_progress() {
    let mut server = Server::new();
    let url = server.url();
    
    let _m = server
        .mock("GET", "/api/data/1")
        .with_status(200)
        .with_header("content-length", "100")
        .with_body(vec![0u8; 100])
        .create();
    
    let client = create_test_client(&url);
    
    let temp_dir = tempfile::tempdir().unwrap();
    let output_path = temp_dir.path().join("downloaded.bin");
    
    let mut progress_called = false;
    let progress = Box::new(|current: u64, total: u64| {
        progress_called = true;
        assert!(current <= total);
    });
    
    client
        .download_data(1, &output_path, Some(progress))
        .await
        .unwrap();
    
    let content = tokio::fs::read(&output_path).await.unwrap();
    assert_eq!(content.len(), 100);
}

#[tokio::test]
async fn test_video_stream_with_range() {
    let mut server = Server::new();
    let url = server.url();
    
    let _m = server
        .mock("GET", "/api/data/1")
        .match_header("Range", "bytes=0-1023")
        .with_status(206)
        .with_header("content-range", "bytes 0-1023/2048")
        .with_header("content-length", "1024")
        .with_header("content-type", "video/mp4")
        .with_body(vec![0u8; 1024])
        .create();
    
    let client = create_test_client(&url);
    
    let (data, headers) = client
        .get_video_stream(1, Some(0), Some(1023))
        .await
        .unwrap();
    
    assert_eq!(data.len(), 1024);
    assert_eq!(headers.content_range, Some("bytes 0-1023/2048".to_string()));
    assert_eq!(headers.content_type, Some("video/mp4".to_string()));
}

#[tokio::test]
async fn test_delete_data() {
    let mut server = Server::new();
    let url = server.url();
    
    let _m = server
        .mock("DELETE", "/api/data/1")
        .with_status(200)
        .create();
    
    let client = create_test_client(&url);
    client.delete_data(1).await.unwrap();
}

#[tokio::test]
async fn test_delete_album() {
    let mut server = Server::new();
    let url = server.url();
    
    let _m = server
        .mock("DELETE", "/api/album/1")
        .with_status(200)
        .create();
    
    let client = create_test_client(&url);
    client.delete_album(1).await.unwrap();
}

#[tokio::test]
async fn test_error_unauthorized() {
    let mut server = Server::new();
    let url = server.url();
    
    let _m = server
        .mock("GET", "/api/albums")
        .with_status(401)
        .create();
    
    let client = create_test_client(&url);
    let result = client.get_albums().await;
    
    assert!(matches!(result, Err(zerogallery::Error::Unauthorized)));
}

#[tokio::test]
async fn test_error_not_found() {
    let mut server = Server::new();
    let url = server.url();
    
    let _m = server
        .mock("GET", "/api/data/999")
        .with_status(404)
        .create();
    
    let client = create_test_client(&url);
    let result = client.get_data(999).await;
    
    assert!(matches!(result, Err(zerogallery::Error::NotFound(_))));
}

#[tokio::test]
async fn test_multiple_file_upload() {
    let mut server = Server::new();
    let url = server.url();
    
    let _m = server
        .mock("POST", "/api/upload/1")
        .with_status(200)
        .with_body("[101, 102, 103]")
        .create();
    
    let client = create_test_client(&url);
    
    // Создаем временные файлы
    let temp_dir = tempfile::tempdir().unwrap();
    let files: Vec<_> = (0..3)
        .map(|i| {
            let path = temp_dir.path().join(format!("file{}.txt", i));
            std::fs::write(&path, format!("content {}", i)).unwrap();
            path
        })
        .collect();
    
    let file_refs: Vec<_> = files.iter().map(|p| p.as_path()).collect();
    let ids = client.upload_multiple_files(&file_refs, 1).await.unwrap();
    
    assert_eq!(ids, vec![101, 102, 103]);
}

// Бенчмарки
#[cfg(test)]
mod benches {
    use super::*;
    use criterion::{criterion_group, criterion_main, Criterion};
    
    fn bench_format_size(c: &mut Criterion) {
        let data = DataInfo {
            id: 1,
            album_id: 1,
            size: 1048576,
            created_timestamp: 0,
            name: String::new(),
            extension: String::new(),
            description: String::new(),
            mime_type: String::new(),
            tags: String::new(),
        };
        
        c.bench_function("format_size", |b| {
            b.iter(|| data.format_size())
        });
    }
    
    criterion_group!(benches, bench_format_size);
}