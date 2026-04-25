using System.Reflection;
using System.Text;
using ZeroGallery.Shared.Services;

namespace ZeroGallery.Shared.Tests
{
    /// <summary>
    /// Tests for the internal StreamHelper.Transfer (regression for fix #1: returned 0 instead of totalBytes).
    /// </summary>
    public class StreamHelperTests
    {
        // StreamHelper is internal; reflect the Transfer method to call it from tests.
        private static readonly MethodInfo _transfer = typeof(MediaTypeDetector).Assembly
            .GetType("ZeroGallery.Shared.Services.StreamHelper", throwOnError: true)!
            .GetMethod("Transfer", BindingFlags.NonPublic | BindingFlags.Static)!;

        private static long Transfer(Stream input, Stream output)
        {
            var task = (Task<long>)_transfer.Invoke(null, new object[] { input, output })!;
            return task.GetAwaiter().GetResult();
        }

        [Test]
        public void Transfer_ReturnsTotalBytesCopied()
        {
            var data = Encoding.UTF8.GetBytes(new string('a', 100_000));
            using var src = new MemoryStream(data);
            using var dst = new MemoryStream();

            var copied = Transfer(src, dst);

            Assert.That(copied, Is.EqualTo(data.LongLength));
            Assert.That(dst.ToArray(), Is.EqualTo(data));
        }

        [Test]
        public void Transfer_EmptyStream_ReturnsZero()
        {
            using var src = new MemoryStream(Array.Empty<byte>());
            using var dst = new MemoryStream();

            var copied = Transfer(src, dst);

            Assert.That(copied, Is.EqualTo(0));
            Assert.That(dst.Length, Is.EqualTo(0));
        }

        [Test]
        public void Transfer_SmallerThanBuffer_ReturnsExactCount()
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };
            using var src = new MemoryStream(data);
            using var dst = new MemoryStream();

            var copied = Transfer(src, dst);

            Assert.That(copied, Is.EqualTo(5));
            Assert.That(dst.ToArray(), Is.EqualTo(data));
        }

        [Test]
        public void Transfer_LargerThanBufferMultiple_ReturnsTotal()
        {
            // The internal buffer is 16384; verify multi-iteration loop sums correctly.
            var size = 16384 * 3 + 12345;
            var data = new byte[size];
            new Random(42).NextBytes(data);
            using var src = new MemoryStream(data);
            using var dst = new MemoryStream();

            var copied = Transfer(src, dst);

            Assert.That(copied, Is.EqualTo(size));
            Assert.That(dst.ToArray(), Is.EqualTo(data));
        }
    }
}
