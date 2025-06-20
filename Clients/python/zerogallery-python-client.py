import os
import json
import asyncio
import aiohttp
import aiofiles
from typing import List, Dict, Optional, Union, BinaryIO, Tuple
from dataclasses import dataclass
from pathlib import Path
import logging
from datetime import datetime

# Настройка логирования
logger = logging.getLogger(__name__)


# Модели данных
@dataclass
class AlbumInfo:
    id: int
    image_preview_id: int
    name: str
    description: str
    is_protected: bool

    @classmethod
    def from_dict(cls, data: dict) -> 'AlbumInfo':
        return cls(
            id=data['id'],
            image_preview_id=data['imagePreviewId'],
            name=data['name'],
            description=data.get('description', ''),
            is_protected=data.get('isProtected', False)
        )


@dataclass
class DataInfo:
    id: int
    album_id: int
    size: int
    created_timestamp: int
    name: str
    extension: str
    description: str
    mime_type: str
    tags: str

    @classmethod
    def from_dict(cls, data: dict) -> 'DataInfo':
        return cls(
            id=data['id'],
            album_id=data['albumId'],
            size=data['size'],
            created_timestamp=data['createdTimestamp'],
            name=data['name'],
            extension=data.get('extension', ''),
            description=data.get('description', ''),
            mime_type=data.get('mimeType', ''),
            tags=data.get('tags', '')
        )
    
    @property
    def created_datetime(self) -> datetime:
        """Преобразование timestamp в datetime"""
        return datetime.fromtimestamp(self.created_timestamp / 1000)


@dataclass
class CreateAlbumInfo:
    name: str
    description: str = ""
    token: str = ""
    allow_remove_data: bool = False

    def to_dict(self) -> dict:
        return {
            'name': self.name,
            'description': self.description,
            'token': self.token,
            'allowRemoveData': self.allow_remove_data
        }


class ZeroGalleryClient:
    """Асинхронный клиент для работы с ZeroGallery API"""
    
    def __init__(self, base_url: str, access_token: Optional[str] = None):
        self.base_url = base_url.rstrip('/')
        self.access_token = access_token
        self._session: Optional[aiohttp.ClientSession] = None
    
    async def __aenter__(self):
        await self._ensure_session()
        return self
    
    async def __aexit__(self, exc_type, exc_val, exc_tb):
        await self.close()
    
    async def _ensure_session(self):
        """Создание сессии если она еще не создана"""
        if self._session is None:
            headers = {}
            if self.access_token:
                headers['X-Access-Token'] = self.access_token
            self._session = aiohttp.ClientSession(headers=headers)
    
    async def close(self):
        """Закрытие сессии"""
        if self._session:
            await self._session.close()
            self._session = None
    
    def set_access_token(self, token: str):
        """Установка токена доступа"""
        self.access_token = token
        if self._session:
            self._session.headers['X-Access-Token'] = token
    
    async def _request(self, method: str, endpoint: str, **kwargs) -> Union[dict, list, str, bytes]:
        """Базовый метод для выполнения запросов"""
        await self._ensure_session()
        url = f"{self.base_url}/{endpoint}"
        
        async with self._session.request(method, url, **kwargs) as response:
            if response.status == 401:
                raise PermissionError("Unauthorized access")
            elif response.status == 404:
                raise FileNotFoundError(f"Resource not found: {endpoint}")
            
            response.raise_for_status()
            
            content_type = response.headers.get('Content-Type', '')
            if 'application/json' in content_type:
                return await response.json()
            elif 'text' in content_type:
                return await response.text()
            else:
                return await response.read()
    
    # Версия API
    async def get_version(self) -> str:
        """Получение версии API"""
        return await self._request('GET', 'api/version')
    
    # Работа с альбомами
    async def get_albums(self) -> List[AlbumInfo]:
        """Получение списка альбомов"""
        data = await self._request('GET', 'api/albums')
        return [AlbumInfo.from_dict(item) for item in data]
    
    async def create_album(self, album_info: CreateAlbumInfo) -> AlbumInfo:
        """Создание нового альбома"""
        data = await self._request(
            'POST', 
            'api/album',
            json=album_info.to_dict()
        )
        return AlbumInfo.from_dict(data)
    
    async def delete_album(self, album_id: int):
        """Удаление альбома"""
        await self._request('DELETE', f'api/album/{album_id}')
    
    # Работа с данными
    async def get_data_without_albums(self) -> List[DataInfo]:
        """Получение данных без привязки к альбомам"""
        data = await self._request('GET', 'api/data')
        return [DataInfo.from_dict(item) for item in data]
    
    async def get_album_data(self, album_id: int) -> List[DataInfo]:
        """Получение данных альбома"""
        data = await self._request('GET', f'api/album/{album_id}/data')
        return [DataInfo.from_dict(item) for item in data]
    
    # Загрузка файлов
    async def upload_file(self, file_path: Union[str, Path], album_id: int = -1) -> int:
        """Загрузка одного файла"""
        file_path = Path(file_path)
        
        async with aiofiles.open(file_path, 'rb') as f:
            data = await f.read()
            return await self.upload_file_data(data, file_path.name, album_id)
    
    async def upload_file_data(self, file_data: bytes, filename: str, album_id: int = -1) -> int:
        """Загрузка файла из байтов"""
        data = aiohttp.FormData()
        data.add_field('file', file_data, filename=filename)
        
        endpoint = f'api/upload/{album_id}' if album_id > 0 else 'api/upload'
        result = await self._request('POST', endpoint, data=data)
        return int(result)
    
    async def upload_multiple_files(self, file_paths: List[Union[str, Path]], album_id: int = -1) -> List[int]:
        """Загрузка нескольких файлов"""
        data = aiohttp.FormData()
        
        for file_path in file_paths:
            file_path = Path(file_path)
            async with aiofiles.open(file_path, 'rb') as f:
                file_data = await f.read()
                data.add_field('files', file_data, filename=file_path.name)
        
        endpoint = f'api/upload/{album_id}' if album_id > 0 else 'api/upload'
        result = await self._request('POST', endpoint, data=data)
        return result
    
    # Получение превью
    async def get_preview(self, data_id: int) -> bytes:
        """Получение превью изображения"""
        return await self._request('GET', f'api/preview/{data_id}')
    
    async def save_preview(self, data_id: int, output_path: Union[str, Path]):
        """Сохранение превью в файл"""
        preview_data = await self.get_preview(data_id)
        output_path = Path(output_path)
        
        async with aiofiles.open(output_path, 'wb') as f:
            await f.write(preview_data)
    
    # Получение данных/файлов
    async def get_data(self, data_id: int) -> bytes:
        """Получение данных файла"""
        return await self._request('GET', f'api/data/{data_id}')
    
    async def download_data(self, data_id: int, output_path: Union[str, Path], 
                           progress_callback=None, chunk_size: int = 8192):
        """Скачивание файла с отслеживанием прогресса"""
        await self._ensure_session()
        url = f"{self.base_url}/api/data/{data_id}"
        
        async with self._session.get(url) as response:
            response.raise_for_status()
            
            total_size = int(response.headers.get('Content-Length', 0))
            downloaded = 0
            
            output_path = Path(output_path)
            async with aiofiles.open(output_path, 'wb') as f:
                async for chunk in response.content.iter_chunked(chunk_size):
                    await f.write(chunk)
                    downloaded += len(chunk)
                    
                    if progress_callback and total_size > 0:
                        progress = (downloaded / total_size) * 100
                        await progress_callback(progress)
    
    async def get_video_stream(self, data_id: int, range_start: Optional[int] = None, 
                              range_end: Optional[int] = None) -> Tuple[bytes, Dict[str, str]]:
        """Получение видео с поддержкой Range запросов"""
        await self._ensure_session()
        url = f"{self.base_url}/api/data/{data_id}"
        
        headers = {}
        if range_start is not None or range_end is not None:
            if range_start is not None and range_end is not None:
                headers['Range'] = f'bytes={range_start}-{range_end}'
            elif range_start is not None:
                headers['Range'] = f'bytes={range_start}-'
            else:
                headers['Range'] = f'bytes=-{range_end}'
        
        async with self._session.get(url, headers=headers) as response:
            response.raise_for_status()
            
            response_headers = {
                'Content-Range': response.headers.get('Content-Range', ''),
                'Content-Length': response.headers.get('Content-Length', ''),
                'Content-Type': response.headers.get('Content-Type', '')
            }
            
            data = await response.read()
            return data, response_headers
    
    # Удаление данных
    async def delete_data(self, data_id: int):
        """Удаление файла"""
        await self._request('DELETE', f'api/data/{data_id}')


# Синхронная обертка для удобства использования
class ZeroGalleryClientSync:
    """Синхронная обертка над асинхронным клиентом"""
    
    def __init__(self, base_url: str, access_token: Optional[str] = None):
        self.async_client = ZeroGalleryClient(base_url, access_token)
        self._loop = None
    
    def _run(self, coro):
        """Запуск асинхронной корутины"""
        if self._loop is None:
            self._loop = asyncio.new_event_loop()
        return self._loop.run_until_complete(coro)
    
    def __enter__(self):
        self._run(self.async_client.__aenter__())
        return self
    
    def __exit__(self, exc_type, exc_val, exc_tb):
        self._run(self.async_client.__aexit__(exc_type, exc_val, exc_tb))
        if self._loop:
            self._loop.close()
            self._loop = None
    
    # Проксирование всех методов
    def get_version(self) -> str:
        return self._run(self.async_client.get_version())
    
    def get_albums(self) -> List[AlbumInfo]:
        return self._run(self.async_client.get_albums())
    
    def create_album(self, album_info: CreateAlbumInfo) -> AlbumInfo:
        return self._run(self.async_client.create_album(album_info))
    
    def delete_album(self, album_id: int):
        return self._run(self.async_client.delete_album(album_id))
    
    def get_data_without_albums(self) -> List[DataInfo]:
        return self._run(self.async_client.get_data_without_albums())
    
    def get_album_data(self, album_id: int) -> List[DataInfo]:
        return self._run(self.async_client.get_album_data(album_id))
    
    def upload_file(self, file_path: Union[str, Path], album_id: int = -1) -> int:
        return self._run(self.async_client.upload_file(file_path, album_id))
    
    def upload_multiple_files(self, file_paths: List[Union[str, Path]], album_id: int = -1) -> List[int]:
        return self._run(self.async_client.upload_multiple_files(file_paths, album_id))
    
    def download_data(self, data_id: int, output_path: Union[str, Path]):
        return self._run(self.async_client.download_data(data_id, output_path))
    
    def delete_data(self, data_id: int):
        return self._run(self.async_client.delete_data(data_id))


# Примеры использования
async def async_example():
    """Пример асинхронного использования"""
    async with ZeroGalleryClient('http://localhost:5000', 'your-token') as client:
        # Получение версии
        version = await client.get_version()
        print(f"API Version: {version}")
        
        # Получение альбомов
        albums = await client.get_albums()
        print(f"Found {len(albums)} albums")
        
        # Создание альбома
        new_album = await client.create_album(CreateAlbumInfo(
            name="My Python Album",
            description="Created from Python",
            token="album-token",
            allow_remove_data=True
        ))
        print(f"Created album: {new_album.name} (ID: {new_album.id})")
        
        # Загрузка файла
        file_id = await client.upload_file('image.jpg', new_album.id)
        print(f"Uploaded file ID: {file_id}")
        
        # Загрузка нескольких файлов
        file_ids = await client.upload_multiple_files(['img1.jpg', 'img2.jpg'], new_album.id)
        print(f"Uploaded {len(file_ids)} files")
        
        # Получение данных альбома
        album_data = await client.get_album_data(new_album.id)
        for data in album_data:
            print(f"File: {data.name} ({data.size} bytes)")
        
        # Скачивание с прогрессом
        async def progress(percent):
            print(f"Download progress: {percent:.2f}%")
        
        await client.download_data(file_id, 'downloaded.jpg', progress)
        
        # Получение превью
        await client.save_preview(file_id, 'preview.jpg')
        
        # Получение части видео (Range request)
        video_chunk, headers = await client.get_video_stream(file_id, range_start=0, range_end=1024*1024)
        print(f"Downloaded {len(video_chunk)} bytes, Content-Range: {headers['Content-Range']}")
        
        # Удаление
        await client.delete_data(file_id)
        await client.delete_album(new_album.id)


def sync_example():
    """Пример синхронного использования"""
    with ZeroGalleryClientSync('http://localhost:5000', 'your-token') as client:
        # Получение версии
        version = client.get_version()
        print(f"API Version: {version}")
        
        # Создание альбома
        new_album = client.create_album(CreateAlbumInfo(
            name="Sync Album",
            description="Created synchronously"
        ))
        print(f"Created album: {new_album.name}")
        
        # Загрузка файла
        file_id = client.upload_file('photo.jpg', new_album.id)
        print(f"Uploaded file ID: {file_id}")
        
        # Скачивание
        client.download_data(file_id, 'downloaded_sync.jpg')
        
        # Удаление
        client.delete_data(file_id)
        client.delete_album(new_album.id)


if __name__ == "__main__":
    # Запуск асинхронного примера
    asyncio.run(async_example())
    
    # Или синхронного
    # sync_example()
