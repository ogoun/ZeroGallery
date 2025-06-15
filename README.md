# ZeroGallery
Simple media gallery

Сервис для размещения фото, видео и прочих файлов.
Для загрузки файлов и создания альбомов нужно указывать токен загрузки (передается в заголовке X-ZERO-UPLOAD-TOKEN).
Файлы вне альбомов доступны для просмотра всем. Альбомы можно дополнительно защищать токенов разрешающим просмотр содержимого. Токен для просмотра альбома передается в заголовке X-ZERO-ACCESS-TOKEN.

Для запуска в linux потребуется установить sqlite3.
```shell
apt install libsqlite3-dev
```


Service for posting photos, videos and other files.
To upload files and create albums, you need to specify an upload token (passed in the X-ZERO-UPLOAD-TOKEN header).
Files outside of albums are available for everyone to view. Albums can be additionally protected with a token allowing viewing of the contents. The token for viewing an album is transferred in the header X-ZERO-ACCESS-TOKEN.

To run in Linux, you will need to install sqlite3.
```shell
apt install libsqlite3-dev
```
