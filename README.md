# ZeroGallery
Simple media gallery

![ZeroGallery](https://github.com/ogoun/ogoun/blob/main/images/zerogallery/01.png)

## Русский

Сервис для размещения фото, видео и прочих файлов.
Для загрузки файлов и создания альбомов нужно указывать токен загрузки (поле Токен загрузки).
Файлы вне альбомов доступны для просмотра всем. Альбомы можно дополнительно защищать токенов разрешающим просмотр содержимого.

Для запуска в linux потребуется установить sqlite3.
```shell
apt install libsqlite3-dev
```

### API

#### Структуры данных

##### Альбом
- id - тип long, идентификатор альбома
- imagePreviewId - тип long, идентификатор картинки для превью альбома
- name - название альбома
- description - описание альбома
- isProtected - тип bool,  true если альбом защищен токеном

```json
{
  "id": 1,
  "imagePreviewId": -1,
  "name": "Photo2025",
  "description": "Фотографии 2025г.",
  "isProtected": false
}
```

##### Метаданные файла
- id - тип long, идентификатор файла
- albumId - long, идентификатор альбома к которому относится файл, если файл вне альбома, значение будет -1. Все файлы вне альбомов являются публично доступными.
- size - размер файла в байтах
- createdTimestamp - дата добавления файла (unix timestamp)
- name - название файла
- extension - расширение (определяется автоматически по сигнатуре)
- description - описание
- mimeType - определяется автоматически по сигнатуре
- tags - список тегов через точку с запятой

```json
{
  "id": 1,
  "albumId": -1,
  "size": 0,
  "createdTimestamp": 1749843407768,
  "name": "image.jpg",
  "extension": ".jpg",
  "description": "",
  "mimeType": "image/jpeg",
  "tags": ""
}
```

##### Создание альбома
- name - название альбома
- description - описание альбома
- token - токен доступа к альбому, если оставить пустым, альбом будет доступен всем

```json
{
	"name": "Photo2025",
	"description": "Фотографии 2025г.",
	"token": "super_secret_password!!!"
}
```

#### Токены доступа
Передаются в заголовках запроса.
- **X-ZERO-UPLOAD-TOKEN** - токен разрешающий создание файлов и загрузку файлов
- **X-ZERO-ACCESS-TOKEN** - токен разрешающий доступ к альбому

Соответсвенно, для записи в альбом защищенный токеном, нужно указать оба токена, и для доступа к альбому и для разрешения операций записи.
### Методы API

- GET **/api/albums** - возвращает список альбомов
- GET **/api/data** - возвращает список файлов не привязанных к альбомам
- GET **/api/album/{id}/data** - возвращает список файлов указанного альбома
- GET **/api/preview/{id}** - получить превью небольшой размерности для файла (на текущий момент доступно для .png, .jpg, .bmp, .gif, .heic, .ico, .svg, .tiff, .webp), если файл относится к альбому, требуется передать заголовок **X-ZERO-ACCESS-TOKEN**
- GET **/api/data/{id}** - получить содержимое указанного файла, если файл относится к альбому, требуется передать заголовок **X-ZERO-ACCESS-TOKEN**
- POST **/api/album** - создать альбом, передаваемые поля перечислены в пункте Создание альбома в структурах данных
- POST **/api/upload/{albumId?}** - загрузить новый файл, если albumId не указан или равен -1, файл будет загружен в публичную область видимости вне альбомов и доступен всем
- DELETE **/api/data/{id}** - удаляет указанный файл
- DELETE **/api/album/{id}** - удаляет указанный альбом



## Eng

Service for posting photos, videos and other files.
To upload files and create albums, you need to specify an upload token.
Files outside of albums are available for everyone to view. Albums can be additionally protected with a token allowing viewing of the contents.

To run in Linux, you will need to install sqlite3.
```shell
apt install libsqlite3-dev
```
