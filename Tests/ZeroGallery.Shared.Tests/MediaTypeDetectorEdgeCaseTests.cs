using System.Text;
using ZeroGallery.Shared.Services;

namespace ZeroGallery.Shared.Tests
{
    /// <summary>
    /// Edge cases for MediaTypeDetector that aren't covered by the sample-file roundtrip test.
    /// </summary>
    public class MediaTypeDetectorEdgeCaseTests
    {
        [Test]
        public void Detects_Svg_WithUtf8Bom()
        {
            // Regression for fix #10: SVG saved with UTF-8 BOM was detected as binary.
            byte[] bom = { 0xEF, 0xBB, 0xBF };
            byte[] xml = Encoding.ASCII.GetBytes("<?xml version=\"1.0\"?><svg xmlns=\"http://www.w3.org/2000/svg\"></svg>");
            using var ms = new MemoryStream();
            ms.Write(bom);
            ms.Write(xml);
            ms.Position = 0;

            var info = MediaTypeDetector.GetDataTypeInfo(ms);

            Assert.That(info.Extension, Is.EqualTo(".svg"));
            Assert.That(info.MimeType, Is.EqualTo("image/svg+xml"));
        }

        [Test]
        public void Detects_Svg_WithoutBom_DirectTag()
        {
            byte[] data = Encoding.ASCII.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>");
            using var ms = new MemoryStream(data);
            var info = MediaTypeDetector.GetDataTypeInfo(ms);
            Assert.That(info.Extension, Is.EqualTo(".svg"));
        }

        [Test]
        public void EmptyStream_ReturnsBinary()
        {
            using var ms = new MemoryStream();
            var info = MediaTypeDetector.GetDataTypeInfo(ms);
            Assert.That(info.Extension, Is.EqualTo(".bin"));
        }

        [Test]
        public void Random_Garbage_ReturnsBinary()
        {
            var data = new byte[1024];
            new Random(7).NextBytes(data);
            // Force first 4 bytes to non-magic-matching values to avoid accidental match.
            data[0] = 0x00; data[1] = 0xFF; data[2] = 0x55; data[3] = 0xAA;
            using var ms = new MemoryStream(data);
            var info = MediaTypeDetector.GetDataTypeInfo(ms);
            Assert.That(info.Extension, Is.EqualTo(".bin"));
        }

        [Test]
        public void RegularTiff_NotDetectedAsDng()
        {
            // Regression for fix #20: a plain TIFF that contains the string "Adobe"
            // (typical when saved from Photoshop) must not be misclassified as DNG.
            // Build a minimal valid TIFF header II*\0 with IFD0 at offset 8 containing
            // a single non-DNG tag (0x0100 = ImageWidth), then an "Adobe" marker after IFD0.
            var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);
            bw.Write((byte)0x49); bw.Write((byte)0x49); // "II"
            bw.Write((ushort)0x002A);                    // magic
            bw.Write((uint)8);                           // IFD0 offset = 8
            bw.Write((ushort)1);                         // numEntries
            // Entry: tag=0x0100 ImageWidth, type=3 SHORT, count=1, value=100
            bw.Write((ushort)0x0100);
            bw.Write((ushort)3);
            bw.Write((uint)1);
            bw.Write((uint)100);
            bw.Write((uint)0);                           // next IFD = 0
            bw.Write(Encoding.ASCII.GetBytes("Adobe Photoshop CC 2024 metadata"));
            bw.Flush();
            ms.Position = 0;

            var info = MediaTypeDetector.GetDataTypeInfo(ms);

            Assert.That(info.Extension, Is.EqualTo(".tiff"),
                "A plain TIFF with 'Adobe' string in metadata must not be classified as DNG");
        }

        [Test]
        public void TiffWithDngVersionTag_DetectedAsDng()
        {
            // Build a minimal TIFF with the DNGVersion (0xC612) tag → should be classified as .dng.
            var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);
            bw.Write((byte)0x49); bw.Write((byte)0x49);
            bw.Write((ushort)0x002A);
            bw.Write((uint)8);                           // IFD0 at offset 8
            bw.Write((ushort)1);                         // numEntries
            bw.Write((ushort)0xC612);                    // DNGVersion tag
            bw.Write((ushort)1);                         // type = BYTE
            bw.Write((uint)4);                           // count = 4
            bw.Write(new byte[] { 1, 4, 0, 0 });         // version 1.4.0.0
            bw.Write((uint)0);                           // next IFD
            bw.Flush();
            ms.Position = 0;

            var info = MediaTypeDetector.GetDataTypeInfo(ms);

            Assert.That(info.Extension, Is.EqualTo(".dng"));
        }

        [Test]
        public void MimeTypeDetectorTest_OnAllSampleFiles()
        {
            // Roundtrip across actual sample files in the test bin/Debug/.../images directory.
            // Validates that signature detection survives any future refactor.
            var images = new[]
            {
                "./images/sample.png",
                "./images/sample.jpg",
                "./images/sample.bmp",
                "./images/sample.gif",
                "./images/sample.heic",
                "./images/sample.ico",
                "./images/sample.svg",
                "./images/sample.tiff",
                "./images/sample.webp",
                "./images/sample.cr2",
                "./images/sample.dng",
                "./images/sample.nef",
                "./images/sample.arw",
                "./images/sample.orf",
                "./images/sample.sr2",
                "./images/sample.srf",
            };
            var videos = new[]
            {
                "./images/sample.mov",
                "./images/sample.mp4",
                "./images/sample.avi",
                "./images/sample.webm",
                "./images/sample.wmv",
            };
            foreach (var path in images.Concat(videos))
            {
                if (!File.Exists(path)) continue;
                var ext = Path.GetExtension(path).ToLowerInvariant();
                using var fs = File.OpenRead(path);
                var info = MediaTypeDetector.GetDataTypeInfo(fs);
                Assert.That(info.Extension, Is.EqualTo(ext), $"Mismatch for {path}");
            }
        }
    }
}
