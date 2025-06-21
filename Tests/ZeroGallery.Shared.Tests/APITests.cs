using Microsoft.AspNetCore.Mvc.Testing;
using System.Xml.Linq;
using ZeroGalleryClient;

namespace ZeroGallery.Shared.Tests
{
    internal class APITests
    {
        private readonly List<string> images = new List<string>
        {
            "./images/01.png",
            "./images/02.jpg",
            "./images/03.bmp",
            "./images/04.gif",
            "./images/05.heic",
            "./images/06.ico",
            "./images/07.svg",
            "./images/08.tiff",
            "./images/09.webp",
        };

        private readonly List<string> videos = new List<string>
        {
            "./images/10.mov",
            "./images/11.mp4",
            "./images/12.avi",
            "./images/13.webm",
            "./images/14.wmv",
        };

        private WebApplicationFactory<ZeroGalleryApp.Program> _zeroGalleryApp = default!;
        private ZeroGalleryApiClient _client;

        const string MASTER_TOKEN = "master_token";
        const string UPLOAD_TOKEN = "upload_token";
        const string ALBUM_TOKEN = "album_token";

        [OneTimeSetUp]
        public async Task OneTimeSetup()
        {
            Environment.SetEnvironmentVariable("port", "8088");
            Environment.SetEnvironmentVariable("api_master_token", MASTER_TOKEN);
            Environment.SetEnvironmentVariable("api_write_token", UPLOAD_TOKEN);
            Environment.SetEnvironmentVariable("data_folder", "media");
            Environment.SetEnvironmentVariable("db_path", "db");

            _zeroGalleryApp = new WebApplicationFactory<ZeroGalleryApp.Program>();
            _zeroGalleryApp.Server.CreateHandler();

            Thread.Sleep(1500);
            var httpClient = _zeroGalleryApp.CreateClient();
            _client = new ZeroGalleryApiClient(httpClient, uploadToken: UPLOAD_TOKEN);
            await _client.DeleteAll(MASTER_TOKEN);
        }

        [OneTimeTearDown]
        public async Task OneTimeTearDown()
        {
            await _client.DeleteAll(MASTER_TOKEN);
            _zeroGalleryApp.Dispose();
            _client.Dispose();
        }

        [TearDown]
        public async Task TearDown()
        {
            await _client.DeleteAll(MASTER_TOKEN);
        }

        [Test]
        public async Task MakeAlbumTest()
        {
            var album = await _client.CreateAlbumAsync("1", allowRemoveData: true);

            Assert.That(album.Name, Is.EqualTo("1"));
            Assert.That(album.IsProtected, Is.EqualTo(false));

            var albums = await _client.GetAlbumsAsync();

            Assert.That(albums.Length, Is.EqualTo(1));
            Assert.That(albums[0].Name, Is.EqualTo("1"));

            await _client.DeleteAlbumAsync(albums[0].Id);
            albums = await _client.GetAlbumsAsync();

            Assert.That(albums.Length, Is.EqualTo(0));
        }

        [Test]
        public async Task InsertDataWithoutAlbumTest()
        {
            var identities = new List<long>();
            var names = new List<string>();

            foreach (var image in images)
            {
                using (var fs = File.OpenRead(image))
                {
                    var name = Path.GetFileName(image);
                    identities.Add(await _client.UploadFileAsync(fs, name));
                    names.Add(name);
                }
            }
            foreach (var video in videos)
            {
                using (var fs = File.OpenRead(video))
                {
                    var name = Path.GetFileName(video);
                    identities.Add(await _client.UploadFileAsync(fs, name));
                    names.Add(name);
                }
            }

            var items = await _client.GetNoAlbumDataItemsAsync();

            Assert.That(items.Length, Is.EqualTo(identities.Count));

            foreach (var item in items)
            {
                Assert.That(item.Id, Is.AnyOf(identities));
                Assert.That(item.Name, Is.AnyOf(names));
            }

            foreach (var id in identities)
            {
                await _client.DeleteDataAsync(id);
            }

            items = await _client.GetNoAlbumDataItemsAsync();
            Assert.That(items.Length, Is.EqualTo(0));
        }

        [Test]
        public async Task ProtectedAlbumTest()
        {
            var album = await _client.CreateAlbumAsync("1p", allowRemoveData: true, token: ALBUM_TOKEN);

            Assert.That(album.Name, Is.EqualTo("1p"));
            Assert.That(album.IsProtected, Is.EqualTo(true));

            var albums = await _client.GetAlbumsAsync();

            Assert.That(albums.Length, Is.EqualTo(1));
            Assert.That(albums[0].Name, Is.EqualTo("1p"));

            var dataFile = images[0];
            // NO ACCESS TOKEN
            using (var fs = File.OpenRead(dataFile))
            {
                var name = Path.GetFileName(dataFile);
                var ex = Assert.ThrowsAsync<HttpRequestException>(async () => await _client.UploadFileAsync(fs, name, albumId: album.Id));
                Assert.That(ex.Message, Is.EqualTo("Response status code does not indicate success: 401 (Unauthorized)."));
            }

            // HAS ACCESS TOKEN
            long id;
            using (var fs = File.OpenRead(dataFile))
            {
                var name = Path.GetFileName(dataFile);
                id =  await _client.UploadFileAsync(fs, name, albumId: album.Id, accessToken: ALBUM_TOKEN);
                Assert.That(id, Is.GreaterThan(0));
            }

            // GET WITHOUT ACCESS CODE
            var get_ex = Assert.ThrowsAsync<HttpRequestException>(async () => await _client.GetAlbumDataItemsAsync(album.Id));
            Assert.That(get_ex.Message, Is.EqualTo("Response status code does not indicate success: 401 (Unauthorized)."));

            // GET WITH ACCESS CODE
            var items = await _client.GetAlbumDataItemsAsync(album.Id, accessToken: ALBUM_TOKEN);
            Assert.That(items.Length, Is.EqualTo(1));

            // DELETE ITEM WITHOUT ACCESS CODE
            var del_ex = Assert.ThrowsAsync<HttpRequestException>(async () => await _client.DeleteDataAsync(id));
            Assert.That(del_ex.Message, Is.EqualTo("Response status code does not indicate success: 401 (Unauthorized)."));

            // DELETE ITEM WITH ACCESS CODE
            await _client.DeleteDataAsync(id, accessToken: ALBUM_TOKEN);
            items = await _client.GetAlbumDataItemsAsync(id, accessToken: ALBUM_TOKEN);
            Assert.That(items.Length, Is.EqualTo(0));

            // DELETE ALBUM WITHOUT ACCESS CODE
            var del_alb_ex = Assert.ThrowsAsync<HttpRequestException>(async () => await _client.DeleteAlbumAsync(album.Id));
            Assert.That(del_alb_ex.Message, Is.EqualTo("Response status code does not indicate success: 401 (Unauthorized)."));

            // DELETE ALBUM WITH ACCESS CODE
            await _client.DeleteAlbumAsync(album.Id, accessToken: ALBUM_TOKEN);
            albums = await _client.GetAlbumsAsync();
            Assert.That(albums.Length, Is.EqualTo(0));
        }



        private async Task<bool> TimeoutCheck(TimeSpan timeout, int timeBetweenTryingMs, Func<Task<bool>> test)
        {
            var start = DateTime.UtcNow;
            TimeSpan diff;
            do
            {
                if (await test()) return true;
                diff = DateTime.UtcNow - start;
                await Task.Delay(timeBetweenTryingMs);
            } while (diff < timeout);
            return false;
        }
    }
}
