using ZeroGallery.Shared.Services;

namespace ZeroGallery.Shared.Tests
{
    public class KnownExtensionTests
    {
        [TestCase(".jpg")]
        [TestCase(".png")]
        [TestCase(".bmp")]
        [TestCase(".gif")]
        [TestCase(".heic")]
        [TestCase(".ico")]
        [TestCase(".svg")]
        [TestCase(".tiff")]
        [TestCase(".webp")]
        [TestCase(".dng")]
        [TestCase(".cr2")]
        [TestCase(".nef")]
        [TestCase(".arw")]
        [TestCase(".orf")]
        [TestCase(".sr2")]
        [TestCase(".srf")]
        public void KnownImages_Recognizes(string ext)
        {
            Assert.That(KnownImages.IsImage(ext), Is.True);
        }

        [TestCase(".JPG")]
        [TestCase(".PNG")]
        [TestCase(".HEIC")]
        public void KnownImages_IsCaseInsensitive(string ext)
        {
            Assert.That(KnownImages.IsImage(ext), Is.True);
        }

        [TestCase(".mp4")]
        [TestCase(".pdf")]
        [TestCase(".txt")]
        [TestCase(".unknown")]
        public void KnownImages_RejectsNonImages(string ext)
        {
            Assert.That(KnownImages.IsImage(ext), Is.False);
        }

        [TestCase(".mp4")]
        [TestCase(".mov")]
        [TestCase(".avi")]
        [TestCase(".webm")]
        [TestCase(".wmv")]
        [TestCase(".mkv")]
        public void KnownVideos_Recognizes(string ext)
        {
            Assert.That(KnownVideos.IsVideo(ext), Is.True);
        }

        [TestCase(".MP4")]
        [TestCase(".MOV")]
        public void KnownVideos_IsCaseInsensitive(string ext)
        {
            Assert.That(KnownVideos.IsVideo(ext), Is.True);
        }

        [TestCase(".jpg")]
        [TestCase(".png")]
        [TestCase(".pdf")]
        public void KnownVideos_RejectsNonVideos(string ext)
        {
            Assert.That(KnownVideos.IsVideo(ext), Is.False);
        }
    }
}
