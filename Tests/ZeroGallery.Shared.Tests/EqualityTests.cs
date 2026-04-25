using ZeroGallery.Shared;
using ZeroGallery.Shared.Models.DB;

namespace ZeroGallery.Shared.Tests
{
    /// <summary>
    /// Regression for fix #5: DataRecord.Equals had a broken short-circuit
    /// `ReferenceEquals(null, other)` that should have been `ReferenceEquals(this, other)`.
    /// </summary>
    public class EqualityTests
    {
        private static DataRecord MakeRecord(long id = 1) => new DataRecord
        {
            Id = id,
            AlbumId = 5,
            Size = 1024,
            CreatedTimestamp = 1000,
            ShardIndex = 3,
            Index = 7,
            Name = "x.jpg",
            Extension = ".jpg",
            Description = "d",
            MimeType = "image/jpeg",
            Tags = "t1;t2",
            InRemoving = false,
            PreviewStatus = 0,
            ConvertStatus = 0,
        };

        [Test]
        public void DataRecord_Equals_Null_IsFalse()
        {
            var rec = MakeRecord();
            Assert.That(rec.Equals(null), Is.False);
            Assert.That(rec.Equals((object?)null), Is.False);
        }

        [Test]
        public void DataRecord_Equals_SameInstance_IsTrue()
        {
            var rec = MakeRecord();
            Assert.That(rec.Equals(rec), Is.True);
        }

        [Test]
        public void DataRecord_Equals_SameValues_IsTrue()
        {
            var a = MakeRecord();
            var b = MakeRecord();
            Assert.That(a.Equals(b), Is.True);
        }

        [Test]
        public void DataRecord_Equals_DifferentName_IsFalse()
        {
            var a = MakeRecord();
            var b = MakeRecord();
            b.Name = "other.jpg";
            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void DataRecord_Equals_DifferentId_IsFalse()
        {
            var a = MakeRecord(1);
            var b = MakeRecord(2);
            Assert.That(a.Equals(b), Is.False);
        }

        [Test]
        public void DataAlbum_Equals_Null_IsFalse()
        {
            var alb = new DataAlbum { Id = 1, Name = "n", Description = "d", Token = "t" };
            Assert.That(alb.Equals(null), Is.False);
        }

        [Test]
        public void DataAlbum_Equals_SameInstance_IsTrue()
        {
            var alb = new DataAlbum { Id = 1, Name = "n", Description = "d", Token = "t" };
            Assert.That(alb.Equals(alb), Is.True);
        }

        [Test]
        public void DataAlbum_Equals_SameValues_IsTrue()
        {
            var a = new DataAlbum { Id = 1, Name = "n", Description = "d", Token = "t", ImagePreviewId = -1 };
            var b = new DataAlbum { Id = 1, Name = "n", Description = "d", Token = "t", ImagePreviewId = -1 };
            Assert.That(a.Equals(b), Is.True);
        }

        [Test]
        public void StringExtensions_IsEqual_BothNull_IsTrue()
        {
            string? a = null;
            string? b = null;
            Assert.That(a!.IsEqual(b!), Is.True);
        }

        [Test]
        public void StringExtensions_IsEqual_OneNull_IsFalse()
        {
            string? a = null;
            string b = "x";
            Assert.That(a!.IsEqual(b), Is.False);
            Assert.That(b.IsEqual(a!), Is.False);
        }

        [Test]
        public void StringExtensions_IsEqual_SameValue_IsTrue()
        {
            Assert.That("hello".IsEqual("hello"), Is.True);
        }

        [Test]
        public void StringExtensions_IsEqual_DifferentValues_IsFalse()
        {
            Assert.That("hello".IsEqual("world"), Is.False);
        }
    }
}
