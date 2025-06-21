using ZeroGallery.Shared.Models.DB;
using ZeroGallery.Shared.Services;
using ZeroGallery.Shared.Services.DB;
using ZeroGalleryApp;

namespace ZeroGallery.Shared.Tests
{
    public class Tests
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

        private AppConfig appConfig;
        private DataAlbumRepository albumRepository;
        private DataRecordRepository recordRepository;

        [OneTimeSetUp]
        public void Setup()
        {
            appConfig = new AppConfig
            {
                api_write_token = "test",
                api_master_token = "test",
                data_folder = "media",
                db_path = "db"
            };
            albumRepository = new DataAlbumRepository(appConfig.db_path);
            recordRepository = new DataRecordRepository(appConfig.db_path);
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            albumRepository.Dispose();
            recordRepository.Dispose();
        }

        [Test]
        public void MimeTypeDetectorTest()
        {
            foreach (var image in images)
            {
                using (var fs = File.OpenRead(image))
                {
                    var ext = Path.GetExtension(image).ToLowerInvariant();
                    var info = MediaTypeDetector.GetDataTypeInfo(fs);
                    Assert.That(info.Extension, Is.EqualTo(ext));
                }
            }
            foreach (var video in videos)
            {
                using (var fs = File.OpenRead(video))
                {
                    var ext = Path.GetExtension(video).ToLowerInvariant();
                    var info = MediaTypeDetector.GetDataTypeInfo(fs);
                    Assert.That(info.Extension, Is.EqualTo(ext));
                }
            }
        }

        [Test]
        public async Task DataStorageTest()
        {

            var storage = new DataStorage(appConfig, albumRepository, recordRepository);
            storage.DropAll();
            long lastItemId = -1;

            var inserted = new Dictionary<long, DataRecord>();

            // INSERT ALBUM
            var videoAlbum = storage.AppendAlbum("Video", "Video files", "password", true);
            Assert.That(videoAlbum, Is.Not.Null);
            Assert.That(videoAlbum.Id, Is.GreaterThan(-1));

            // INSERT DATA WITHOUT ALBUM
            foreach (var image in images)
            {
                var name = Path.GetFileName(image);
                using (var fs = File.OpenRead(image))
                {
                    var item = await storage.WriteData(name, name, string.Empty, -1, fs);
                    Assert.That(item, Is.Not.Null);
                    Assert.That(item.Id, Is.GreaterThan(lastItemId));
                    lastItemId = item.Id;
                    inserted.Add(item.Id, item);
                }
            }

            // INSERT DATA WITH ALBUM
            foreach (var video in videos)
            {
                var name = Path.GetFileName(video);
                using (var fs = File.OpenRead(video))
                {
                    var item = await storage.WriteData(name, name, string.Empty, videoAlbum.Id, fs);
                    Assert.That(item, Is.Not.Null);
                    Assert.That(item.Id, Is.GreaterThan(lastItemId));
                    lastItemId = item.Id;
                    inserted.Add(item.Id, item);
                }
            }

            // GET ALL DATA
            var allData = storage.GetAllData();
            foreach (var data in allData)
            {
                Assert.That(inserted.ContainsKey(data.Id));
                Assert.That(inserted[data.Id].Equals(data));
            }

            // GET ALBUMS
            var albums = storage.GetAlbums();
            Assert.That(albums.Any(), Is.True);
            Assert.That(albums.Count(), Is.EqualTo(1));

            // VALIDATE ALBUM
            var album = albums.First();
            Assert.That(album, Is.Not.Null);
            Assert.That(album.Equals(videoAlbum));

            // GET DATA WITHOUT ALBUMS
            var expectedNoAlbumData = inserted.Where(kv => kv.Value.AlbumId == -1)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            var noAlbumData = storage.GetDataWithoutAlbums();
            foreach (var data in noAlbumData)
            {
                Assert.That(expectedNoAlbumData.ContainsKey(data.Id));
                Assert.That(expectedNoAlbumData[data.Id].Equals(data));
            }

            // GET ALBUM DATA
            var expectedAlbumData = inserted.Where(kv => kv.Value.AlbumId != -1)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            var albumData = storage.GetDataByAlbum(album.Id);
            foreach (var data in albumData)
            {
                Assert.That(expectedAlbumData.ContainsKey(data.Id));
                Assert.That(expectedAlbumData[data.Id].Equals(data));
            }


            storage.DropAll();
        }
    }
}
