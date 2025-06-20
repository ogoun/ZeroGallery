package main

import (
	"fmt"
	"log"
	"os"
	"path/filepath"
	"time"

	"github.com/yourusername/zerogallery"
)

func main() {
	// Настройки подключения
	baseURL := "http://localhost:5000"
	accessToken := "your-access-token"

	// Создание клиента
	client := zerogallery.NewClient(baseURL, accessToken)

	// Пример с кастомным HTTP клиентом
	// httpClient := &http.Client{
	//     Timeout: 60 * time.Second,
	// }
	// client.SetHTTPClient(httpClient)

	fmt.Println("🚀 Подключение к ZeroGallery API...")

	// 1. Проверка версии
	version, err := client.GetVersion()
	if err != nil {
		log.Fatalf("❌ Ошибка получения версии: %v", err)
	}
	fmt.Printf("✅ Версия API: %s\n", version)

	// 2. Получение списка альбомов
	fmt.Println("\n📁 Список альбомов:")
	albums, err := client.GetAlbums()
	if err != nil {
		log.Printf("Ошибка получения альбомов: %v", err)
	} else {
		for _, album := range albums {
			protected := "🔓"
			if album.IsProtected {
				protected = "🔒"
			}
			fmt.Printf("  %s %s (ID: %d)\n", protected, album.Name, album.ID)
		}
	}

	// 3. Создание нового альбома
	fmt.Println("\n📝 Создание нового альбома...")
	newAlbum, err := client.CreateAlbum(zerogallery.CreateAlbumInfo{
		Name:            "Test Album from Go",
		Description:     "Тестовый альбом созданный через Go API",
		Token:           "secret-token",
		AllowRemoveData: true,
	})
	if err != nil {
		log.Fatalf("❌ Ошибка создания альбома: %v", err)
	}
	fmt.Printf("✅ Альбом создан: %s (ID: %d)\n", newAlbum.Name, newAlbum.ID)

	// 4. Загрузка файлов
	fmt.Println("\n📤 Загрузка файлов...")

	// Создание тестового файла
	testFile := "test_file.txt"
	err = os.WriteFile(testFile, []byte("Test content from Go client"), 0644)
	if err != nil {
		log.Printf("Ошибка создания тестового файла: %v", err)
	}
	defer os.Remove(testFile)

	// Загрузка одного файла
	fileID, err := client.UploadFile(testFile, newAlbum.ID)
	if err != nil {
		log.Printf("Ошибка загрузки файла: %v", err)
	} else {
		fmt.Printf("✅ Загружен файл %s (ID: %d)\n", testFile, fileID)
	}

	// Загрузка нескольких файлов
	testFiles := []string{"test1.txt", "test2.txt", "test3.txt"}
	for i, fname := range testFiles {
		content := fmt.Sprintf("Test file %d content", i+1)
		os.WriteFile(fname, []byte(content), 0644)
		defer os.Remove(fname)
	}

	fileIDs, err := client.UploadMultipleFiles(testFiles, newAlbum.ID)
	if err != nil {
		log.Printf("Ошибка загрузки файлов: %v", err)
	} else {
		fmt.Printf("✅ Загружено %d файлов\n", len(fileIDs))
	}

	// 5. Просмотр содержимого альбома
	fmt.Println("\n📋 Содержимое альбома:")
	albumData, err := client.GetAlbumData(newAlbum.ID)
	if err != nil {
		log.Printf("Ошибка получения данных альбома: %v", err)
	} else {
		for _, item := range albumData {
			fmt.Printf("  📄 %s (%s) - %s\n", item.Name, item.FormatSize(), item.MimeType)
			fmt.Printf("     Создан: %s\n", item.GetCreatedTime().Format("2006-01-02 15:04:05"))
		}
	}

	// 6. Скачивание файла с прогрессом
	if len(albumData) > 0 {
		fmt.Println("\n📥 Скачивание файла...")
		downloadItem := albumData[0]

		progressFunc := func(current, total int64) {
			if total > 0 {
				percent := float64(current) / float64(total) * 100
				fmt.Printf("\r  Прогресс: %.1f%% [%d/%d bytes]", percent, current, total)
			}
		}

		outputPath := fmt.Sprintf("downloaded_%s", downloadItem.Name)
		err = client.DownloadData(downloadItem.ID, outputPath, progressFunc)
		if err != nil {
			log.Printf("\nОшибка скачивания: %v", err)
		} else {
			fmt.Printf("\n✅ Файл сохранен: %s\n", outputPath)
			defer os.Remove(outputPath)
		}

		// 7. Получение превью (для изображений)
		if downloadItem.MimeType[:5] == "image" {
			previewPath := fmt.Sprintf("preview_%s", downloadItem.Name)
			err = client.SavePreview(downloadItem.ID, previewPath)
			if err != nil {
				log.Printf("Ошибка сохранения превью: %v", err)
			} else {
				fmt.Printf("✅ Превью сохранено: %s\n", previewPath)
				defer os.Remove(previewPath)
			}
		}
	}

	// 8. Работа с видео (Range requests)
	fmt.Println("\n🎥 Пример работы с видео (Range requests)...")
	// Создаем фейковый видео файл для примера
	videoData := make([]byte, 10*1024*1024) // 10 MB
	for i := range videoData {
		videoData[i] = byte(i % 256)
	}
	
	videoID, err := client.UploadFileReader(
		bytes.NewReader(videoData),
		"test_video.mp4",
		newAlbum.ID,
	)
	if err == nil {
		// Получаем первый мегабайт видео
		rangeStart := int64(0)
		rangeEnd := int64(1024 * 1024)
		
		stream, headers, err := client.GetVideoStream(videoID, &zerogallery.VideoStreamOptions{
			RangeStart: &rangeStart,
			RangeEnd:   &rangeEnd,
		})
		
		if err != nil {
			log.Printf("Ошибка получения видео потока: %v", err)
		} else {
			defer stream.Close()
			
			// Читаем данные
			data := make([]byte, 1024*1024)
			n, _ := stream.Read(data)
			
			fmt.Printf("✅ Получен фрагмент видео: %d байт\n", n)
			fmt.Printf("   Content-Range: %s\n", headers["Content-Range"])
			fmt.Printf("   Content-Type: %s\n", headers["Content-Type"])
		}
	}

	// 9. Пример обработки ошибок
	fmt.Println("\n🔍 Пример обработки ошибок...")
	
	// Попытка доступа к несуществующему альбому
	_, err = client.GetAlbumData(999999)
	if err != nil {
		fmt.Printf("⚠️  Ожидаемая ошибка: %v\n", err)
	}

	// 10. Очистка
	fmt.Println("\n🧹 Очистка...")
	fmt.Print("Удалить созданные данные? (y/n): ")
	
	var cleanup string
	fmt.Scanln(&cleanup)
	
	if cleanup == "y" || cleanup == "Y" {
		// Удаление файлов
		for _, item := range albumData {
			err := client.DeleteData(item.ID)
			if err != nil {
				log.Printf("Ошибка удаления файла %s: %v", item.Name, err)
			} else {
				fmt.Printf("  ✅ Удален файл: %s\n", item.Name)
			}
		}

		// Удаление альбома
		err = client.DeleteAlbum(newAlbum.ID)
		if err != nil {
			log.Printf("Ошибка удаления альбома: %v", err)
		} else {
			fmt.Printf("  ✅ Удален альбом: %s\n", newAlbum.Name)
		}
	}

	fmt.Println("\n✨ Готово!")
}

// Дополнительные примеры использования

// Пример работы с большими файлами
func uploadLargeFile(client *zerogallery.Client, filePath string, albumID int64) error {
	file, err := os.Open(filePath)
	if err != nil {
		return err
	}
	defer file.Close()

	stat, err := file.Stat()
	if err != nil {
		return err
	}

	fmt.Printf("Загрузка большого файла: %s (%d MB)\n", 
		filepath.Base(filePath), 
		stat.Size()/(1024*1024))

	start := time.Now()
	fileID, err := client.UploadFileReader(file, filepath.Base(filePath), albumID)
	if err != nil {
		return err
	}

	elapsed := time.Since(start)
	speed := float64(stat.Size()) / elapsed.Seconds() / (1024 * 1024)
	
	fmt.Printf("Файл загружен за %v (%.2f MB/s), ID: %d\n", elapsed, speed, fileID)
	return nil
}

// Пример параллельной загрузки файлов
func uploadFilesParallel(client *zerogallery.Client, filePaths []string, albumID int64) error {
	type result struct {
		path string
		id   int64
		err  error
	}

	results := make(chan result, len(filePaths))

	for _, path := range filePaths {
		go func(p string) {
			id, err := client.UploadFile(p, albumID)
			results <- result{path: p, id: id, err: err}
		}(path)
	}

	for i := 0; i < len(filePaths); i++ {
		res := <-results
		if res.err != nil {
			fmt.Printf("❌ Ошибка загрузки %s: %v\n", res.path, res.err)
		} else {
			fmt.Printf("✅ Загружен %s (ID: %d)\n", res.path, res.id)
		}
	}

	return nil
}

// Пример работы с метаданными
func printDetailedInfo(data *zerogallery.DataInfo) {
	fmt.Printf("Детальная информация о файле:\n")
	fmt.Printf("  ID:          %d\n", data.ID)
	fmt.Printf("  Имя:         %s\n", data.Name)
	fmt.Printf("  Расширение:  %s\n", data.Extension)
	fmt.Printf("  Размер:      %s (%d bytes)\n", data.FormatSize(), data.Size)
	fmt.Printf("  MIME тип:    %s\n", data.MimeType)
	fmt.Printf("  Альбом ID:   %d\n", data.AlbumID)
	fmt.Printf("  Создан:      %s\n", data.GetCreatedTime().Format(time.RFC3339))
	
	if data.Description != "" {
		fmt.Printf("  Описание:    %s\n", data.Description)
	}
	
	if data.Tags != "" {
		fmt.Printf("  Теги:        %s\n", data.Tags)
	}
}