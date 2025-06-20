using Microsoft.AspNetCore.Mvc.Testing;
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

        [Test]
        public async Task TestMakeAlbum()
        {
            var album = await _client.CreateAlbumAsync("1", allowRemoveData: true);

            Assert.That(album.Name.IsEqual("1"), Is.True);
            Assert.That(album.IsProtected == false, Is.True);

            var albums = await _client.GetAlbumsAsync();
            Assert.That(albums.Length == 1, Is.True);

            Assert.That(albums[0].Name.IsEqual("1"), Is.True);

            await _client.DeleteAlbumAsync(albums[0].Id);

            var success = await TimeoutCheck(TimeSpan.FromSeconds(10), 500, async () =>
            {
                albums = await _client.GetAlbumsAsync();
                return albums.Length == 0;
            });
            
            Assert.That(success, Is.True);
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
