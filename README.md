# ZeroGallery
Simple media gallery

![ZeroGallery](https://github.com/ogoun/ogoun/blob/main/images/zerogallery/02.png)

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


| METHOD | PATH                       | DESCRIPTION                                                                                                                                                                                                                   |
| ------ | -------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| GET    | **/api/albums**            | возвращает список альбомов                                                                                                                                                                                                    |
| GET    | **/api/data**              | возвращает список файлов не привязанных к альбомам                                                                                                                                                                            |
| GET    | **/api/album/{id}/data**   | возвращает список файлов указанного альбома                                                                                                                                                                                   |
| GET    | **/api/preview/{id}**      | получить превью небольшой размерности для файла (на текущий момент доступно для .png, .jpg, .bmp, .gif, .heic, .ico, .svg, .tiff, .webp), если файл относится к альбому, требуется передать заголовок **X-ZERO-ACCESS-TOKEN** |
| GET    | **/api/data/{id}**         | получить содержимое указанного файла, если файл относится к альбому, требуется передать заголовок **X-ZERO-ACCESS-TOKEN**                                                                                                     |
| POST   | **/api/album**             | создать альбом, передаваемые поля перечислены в пункте Создание альбома в структурах данных, требуется заголовок **X-ZERO-UPLOAD-TOKEN**                                                                                      |
| POST   | **/api/upload/{albumId?}** | загрузить новый файл, если albumId не указан или равен -1, файл будет загружен в публичную область видимости вне альбомов и доступен всем, требуется заголовок **X-ZERO-UPLOAD-TOKEN**                                        |
| DELETE | **/api/data/{id}**         | удаляет указанный файл                                                                                                                                                                                                        |
| DELETE | **/api/album/{id}**        | удаляет указанный альбом                                                                                                                                                                                                      |




## Eng

Service for posting photos, videos and other files.
To upload files and create albums, you need to specify an upload token.
Files outside of albums are available for everyone to view. Albums can be additionally protected with a token allowing viewing of the contents.

To run in Linux, you will need to install sqlite3.
```shell
apt install libsqlite3-dev
```
### API
#### Data Structures
##### Album
- id - type long, album identifier
- imagePreviewId - type long, identifier of the image for album preview
- name - album name
- description - album description
- isProtected - type bool, true if the album is protected by token
```json
{
  "id": 1,
  "imagePreviewId": -1,
  "name": "Photo2025",
  "description": "Photos 2025",
  "isProtected": false
}
```
##### File Metadata
- id - type long, file identifier
- albumId - long, identifier of the album to which the file belongs, if the file is outside an album, the value will be -1. All files outside of albums are publicly accessible.
- size - file size in bytes
- createdTimestamp - file upload date (unix timestamp)
- name - file name
- extension - extension (determined automatically by signature)
- description - description
- mimeType - determined automatically by signature
- tags - list of tags separated by semicolons
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
##### Album Creation
- name - album name
- description - album description
- token - album access token, if left empty, the album will be accessible to everyone
```json
{
	"name": "Photo2025",
	"description": "Photos 2025",
	"token": "super_secret_password!!!"
}
```
#### Access Tokens
Passed in request headers.
- **X-ZERO-UPLOAD-TOKEN** - token that allows file creation and file uploads
- **X-ZERO-ACCESS-TOKEN** - token that allows access to the album
Accordingly, to write to an album protected by a token, you need to specify both tokens, both for album access and for write operation permissions.
### API Methods
| METHOD | PATH                       | DESCRIPTION                                                                                                                                                                                                            |
| ------ | -------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| GET    | **/api/albums**            | returns list of albums                                                                                                                                                                                                 |
| GET    | **/api/data**              | returns list of files not linked to albums                                                                                                                                                                            |
| GET    | **/api/album/{id}/data**   | returns list of files for the specified album                                                                                                                                                                         |
| GET    | **/api/preview/{id}**      | get small size preview for file (currently available for .png, .jpg, .bmp, .gif, .heic, .ico, .svg, .tiff, .webp), if file belongs to an album, requires **X-ZERO-ACCESS-TOKEN** header                            |
| GET    | **/api/data/{id}**         | get contents of the specified file, if file belongs to an album, requires **X-ZERO-ACCESS-TOKEN** header                                                                                                              |
| POST   | **/api/album**             | create album, transmitted fields are listed in the Album Creation section in data structures, requires **X-ZERO-UPLOAD-TOKEN** header                                                                                |
| POST   | **/api/upload/{albumId?}** | upload new file, if albumId is not specified or equals -1, file will be uploaded to public visibility area outside of albums and accessible to everyone, requires **X-ZERO-UPLOAD-TOKEN** header                      |
| DELETE | **/api/data/{id}**         | deletes the specified file                                                                                                                                                                                            |
| DELETE | **/api/album/{id}**        | deletes the specified album                                                                                                                                                                                           |
