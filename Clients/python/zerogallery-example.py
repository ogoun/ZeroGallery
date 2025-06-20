#!/usr/bin/env python3
"""
Простой пример использования ZeroGallery Python Client
"""

import asyncio
from pathlib import Path
from zerogallery_client import (
    ZeroGalleryClient, 
    ZeroGalleryClientSync,
    CreateAlbumInfo
)


async def main():
    """Основной пример с асинхронным клиентом"""
    
    # Настройки подключения
    BASE_URL = "http://localhost:5000"
    ACCESS_TOKEN = "your-access-token"  # Замените на ваш токен
    
    # Создаем клиент
    async with ZeroGalleryClient(BASE_URL, ACCESS_TOKEN) as client:
        print("🚀 Подключение к ZeroGallery API...")
        
        # 1. Проверка версии
        try:
            version = await client.get_version()
            print(f"✅ Версия API: {version}")
        except Exception as e:
            print(f"❌ Ошибка подключения: {e}")
            return
        
        # 2. Получение списка альбомов
        print("\n📁 Список альбомов:")
        albums = await client.get_albums()
        for album in albums:
            protected = "🔒" if album.is_protected else "🔓"
            print(f"  {protected} {album.name} (ID: {album.id})")
        
        # 3. Создание нового альбома
        print("\n📝 Создание нового альбома...")
        new_album = await client.create_album(CreateAlbumInfo(
            name="Test Album from Python",
            description="Тестовый альбом созданный через Python API",
            token="secret-token",  # Токен для защиты альбома
            allow_remove_data=True
        ))
        print(f"✅ Альбом создан: {new_album.name} (ID: {new_album.id})")
        
        # 4. Загрузка файлов
        print("\n📤 Загрузка файлов...")
        
        # Подготовка тестовых файлов (замените на реальные пути)
        test_files = [
            "test_image1.jpg",
            "test_image2.png",
            "test_video.mp4"
        ]
        
        # Проверяем существование файлов
        existing_files = [f for f in test_files if Path(f).exists()]
        
        if existing_files:
            # Загрузка одного файла
            first_file = existing_files[0]
            file_id = await client.upload_file(first_file, new_album.id)
            print(f"✅ Загружен файл {first_file} (ID: {file_id})")
            
            # Загрузка нескольких файлов
            if len(existing_files) > 1:
                file_ids = await client.upload_multiple_files(existing_files[1:], new_album.id)
                print(f"✅ Загружено {len(file_ids)} файлов")
        else:
            print("⚠️  Тестовые файлы не найдены")
            
            # Создаем и загружаем тестовый файл
            test_data = b"Test file content from Python"
            file_id = await client.upload_file_data(test_data, "test.txt", new_album.id)
            print(f"✅ Создан и загружен тестовый файл (ID: {file_id})")
        
        # 5. Просмотр содержимого альбома
        print("\n📋 Содержимое альбома:")
        album_data = await client.get_album_data(new_album.id)
        for item in album_data:
            size_kb = item.size / 1024
            print(f"  📄 {item.name} ({size_kb:.2f} KB) - {item.mime_type}")
            print(f"     Создан: {item.created_datetime}")
        
        # 6. Скачивание файла с прогрессом
        if album_data:
            print("\n📥 Скачивание файла...")
            download_item = album_data[0]
            
            async def show_progress(percent):
                bar_length = 40
                filled = int(bar_length * percent / 100)
                bar = '█' * filled + '░' * (bar_length - filled)
                print(f"\r  [{bar}] {percent:.1f}%", end='', flush=True)
            
            output_path = f"downloaded_{download_item.name}"
            await client.download_data(download_item.id, output_path, show_progress)
            print(f"\n✅ Файл сохранен: {output_path}")
            
            # 7. Получение превью (для изображений)
            if download_item.mime_type.startswith('image/'):
                preview_path = f"preview_{download_item.name}"
                await client.save_preview(download_item.id, preview_path)
                print(f"✅ Превью сохранено: {preview_path}")
        
        # 8. Работа с видео (Range requests)
        video_items = [item for item in album_data if item.mime_type.startswith('video/')]
        if video_items:
            print("\n🎥 Работа с видео...")
            video = video_items[0]
            
            # Получаем первый мегабайт видео
            chunk, headers = await client.get_video_stream(
                video.id, 
                range_start=0, 
                range_end=1024*1024
            )
            print(f"✅ Получен фрагмент видео: {len(chunk)} байт")
            print(f"   Content-Range: {headers['Content-Range']}")
        
        # 9. Очистка (опционально)
        print("\n🧹 Очистка...")
        cleanup = input("Удалить созданные данные? (y/n): ")
        if cleanup.lower() == 'y':
            # Удаление файлов
            for item in album_data:
                await client.delete_data(item.id)
                print(f"  ✅ Удален файл: {item.name}")
            
            # Удаление альбома
            await client.delete_album(new_album.id)
            print(f"  ✅ Удален альбом: {new_album.name}")
        
        print("\n✨ Готово!")


def sync_quick_example():
    """Простой синхронный пример"""
    print("🔄 Синхронный пример...")
    
    with ZeroGalleryClientSync("http://localhost:5000", "your-token") as client:
        # Получение версии
        version = client.get_version()
        print(f"Версия API: {version}")
        
        # Получение альбомов
        albums = client.get_albums()
        print(f"Найдено альбомов: {len(albums)}")
        
        # Быстрая загрузка и скачивание
        if albums:
            album = albums[0]
            
            # Загрузка
            with open("test.txt", "w") as f:
                f.write("Test content")
            
            file_id = client.upload_file("test.txt", album.id)
            print(f"Загружен файл ID: {file_id}")
            
            # Скачивание
            client.download_data(file_id, "downloaded_test.txt")
            print("Файл скачан")
            
            # Удаление
            client.delete_data(file_id)
            print("Файл удален")


if __name__ == "__main__":
    # Выбор режима
    print("ZeroGallery Python Client - Примеры")
    print("1. Асинхронный пример (рекомендуется)")
    print("2. Синхронный пример")
    
    choice = input("\nВыберите режим (1/2): ")
    
    if choice == "2":
        sync_quick_example()
    else:
        asyncio.run(main())
