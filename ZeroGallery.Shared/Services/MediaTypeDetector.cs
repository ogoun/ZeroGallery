using System.Text;
using ZeroGallery.Shared.Models;

namespace ZeroGallery.Shared.Services
{
    public static class MediaTypeDetector
    {
        private static ImageTypeInfo _unknown = new ImageTypeInfo(".bin", "application/x-binary");
        private class FileSignature
        {
            public byte[] Signature { get; set; }
            public int Offset { get; set; }
            public string Extension { get; set; }
            public string MimeType { get; set; }
            public Func<byte[], bool> AdditionalCheck { get; set; }
        }

        private static readonly List<FileSignature> signatures = new List<FileSignature>
        {
            // JPEG - различные варианты
            new FileSignature { Signature = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, Offset = 0, Extension = ".jpg", MimeType = "image/jpeg" },
            new FileSignature { Signature = new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 }, Offset = 0, Extension = ".jpg", MimeType = "image/jpeg" },
            new FileSignature { Signature = new byte[] { 0xFF, 0xD8, 0xFF, 0xDB }, Offset = 0, Extension = ".jpg", MimeType = "image/jpeg" },
            new FileSignature { Signature = new byte[] { 0xFF, 0xD8, 0xFF, 0xEE }, Offset = 0, Extension = ".jpg", MimeType = "image/jpeg" },
        
            // PNG
            new FileSignature { Signature = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, Offset = 0, Extension = ".png", MimeType = "image/png" },
        
            // GIF
            new FileSignature { Signature = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, Offset = 0, Extension = ".gif", MimeType = "image/gif" },
            new FileSignature { Signature = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, Offset = 0, Extension = ".gif", MimeType = "image/gif" },
        
            // BMP
            new FileSignature { Signature = new byte[] { 0x42, 0x4D }, Offset = 0, Extension = ".bmp", MimeType = "image/bmp" },
        
            // WebP
            new FileSignature { Signature = new byte[] { 0x52, 0x49, 0x46, 0x46 }, Offset = 0, Extension = ".webp", MimeType = "image/webp",
            AdditionalCheck = buffer => buffer.Length >= 12 &&
                              buffer[8] == 0x57 && buffer[9] == 0x45 &&
                              buffer[10] == 0x42 && buffer[11] == 0x50
            },
        
            // ===== RAW ФОРМАТЫ =====
            
            // Canon CR2 - наиболее определённый формат с уникальной сигнатурой на позиции 8
            new FileSignature {
                Signature = new byte[] { 0x49, 0x49, 0x2A, 0x00 },
                Offset = 0,
                Extension = ".cr2",
                MimeType = "image/x-canon-cr2",
                AdditionalCheck = buffer => buffer.Length >= 12 &&
                                  buffer[8] == 0x43 && buffer[9] == 0x52 && // ASCII "CR"
                                  buffer[10] == 0x02 && buffer[11] == 0x00  // Version 2.0
            },
            
            // Olympus ORF - уникальные сигнатуры
            // Big-endian версия
            new FileSignature { Signature = new byte[] { 0x4D, 0x4D, 0x4F, 0x52 }, Offset = 0, Extension = ".orf", MimeType = "image/x-olympus-orf" }, // "MMOR"
            // Little-endian версия
            new FileSignature { Signature = new byte[] { 0x49, 0x49, 0x52, 0x4F }, Offset = 0, Extension = ".orf", MimeType = "image/x-olympus-orf" }, // "IIRO"
            // Вариант IIRS
            new FileSignature { Signature = new byte[] { 0x49, 0x49, 0x52, 0x53 }, Offset = 0, Extension = ".orf", MimeType = "image/x-olympus-orf" }, // "IIRS"
            
            // DNG (Digital Negative) - использует стандартные TIFF заголовки с проверкой содержимого
            new FileSignature {
                Signature = new byte[] { 0x49, 0x49, 0x2A, 0x00 },
                Offset = 0,
                Extension = ".dng",
                MimeType = "image/x-adobe-dng",
                AdditionalCheck = buffer => CheckDngFormat(buffer)
            },
            new FileSignature {
                Signature = new byte[] { 0x4D, 0x4D, 0x00, 0x2A },
                Offset = 0,
                Extension = ".dng",
                MimeType = "image/x-adobe-dng",
                AdditionalCheck = buffer => CheckDngFormat(buffer)
            },
            
            // Nikon NEF - использует TIFF заголовки, требует дополнительной проверки
            // Big-endian (наиболее распространённый)
            new FileSignature {
                Signature = new byte[] { 0x4D, 0x4D, 0x00, 0x2A },
                Offset = 0,
                Extension = ".nef",
                MimeType = "image/x-nikon-nef",
                AdditionalCheck = buffer => CheckNefFormat(buffer)
            },
            // Little-endian (некоторые модели Coolpix)
            new FileSignature {
                Signature = new byte[] { 0x49, 0x49, 0x2A, 0x00 },
                Offset = 0,
                Extension = ".nef",
                MimeType = "image/x-nikon-nef",
                AdditionalCheck = buffer => CheckNefFormat(buffer)
            },
            
            // Sony SR2 / SRF идут ПЕРЕД ARW, потому что у них проверки строже
            // (требуют конкретных моделей DSC-R1 / DSC-F828), а ARW — generic fallback по строке "SONY".

            // Sony SR2 - использует TIFF заголовок с проверкой
            new FileSignature {
                Signature = new byte[] { 0x49, 0x49, 0x2A, 0x00 },
                Offset = 0,
                Extension = ".sr2",
                MimeType = "image/x-sony-sr2",
                AdditionalCheck = buffer => CheckSr2Format(buffer)
            },

            // Sony SRF - использует TIFF заголовок с проверкой
            new FileSignature {
                Signature = new byte[] { 0x49, 0x49, 0x2A, 0x00 },
                Offset = 0,
                Extension = ".srf",
                MimeType = "image/x-sony-srf",
                AdditionalCheck = buffer => CheckSrfFormat(buffer)
            },

            // Sony ARW - использует TIFF заголовок с проверкой
            new FileSignature {
                Signature = new byte[] { 0x49, 0x49, 0x2A, 0x00 },
                Offset = 0,
                Extension = ".arw",
                MimeType = "image/x-sony-arw",
                AdditionalCheck = buffer => CheckArwFormat(buffer)
            },
        
            // TIFF - Little Endian
            new FileSignature { Signature = new byte[] { 0x49, 0x49, 0x2A, 0x00 },Offset = 0, Extension = ".tiff", MimeType = "image/tiff"  },
            // TIFF - Big Endian
            new FileSignature { Signature = new byte[] { 0x4D, 0x4D, 0x00, 0x2A }, Offset = 0, Extension = ".tiff", MimeType = "image/tiff" },
            // BigTIFF - Little Endian
            new FileSignature { Signature = new byte[] { 0x49, 0x49, 0x2B, 0x00 }, Offset = 0, Extension = ".tiff", MimeType = "image/tiff" },
            // BigTIFF - Big Endian
            new FileSignature { Signature = new byte[] { 0x4D, 0x4D, 0x00, 0x2B }, Offset = 0, Extension = ".tiff", MimeType = "image/tiff" },
        
            // ICO
            new FileSignature { Signature = new byte[] { 0x00, 0x00, 0x01, 0x00 }, Offset = 0, Extension = ".ico", MimeType = "image/vnd.microsoft.icon" },
        
            // SVG - XML declaration
            new FileSignature { Signature = new byte[] { 0x3C, 0x3F, 0x78, 0x6D, 0x6C, 0x20 }, Offset = 0, Extension = ".svg", MimeType = "image/svg+xml" },
            // SVG - XML declaration with UTF-8 BOM
            new FileSignature { Signature = new byte[] { 0x3C, 0x3F, 0x78, 0x6D, 0x6C, 0x20 }, Offset = 3, Extension = ".svg", MimeType = "image/svg+xml",
                AdditionalCheck = buffer => buffer.Length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF },
            // SVG - direct tag
            new FileSignature { Signature = new byte[] { 0x3C, 0x73, 0x76, 0x67, 0x20 }, Offset = 0, Extension = ".svg", MimeType = "image/svg+xml" },
            // SVG - direct tag with UTF-8 BOM
            new FileSignature { Signature = new byte[] { 0x3C, 0x73, 0x76, 0x67, 0x20 }, Offset = 3, Extension = ".svg", MimeType = "image/svg+xml",
                AdditionalCheck = buffer => buffer.Length >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF },
        
            // HEIF/HEIC
            new FileSignature { Signature = new byte[] { 0x66, 0x74, 0x79, 0x70, 0x6D, 0x69, 0x66, 0x31 }, Offset = 4, Extension = ".heif", MimeType = "image/heif" },
            new FileSignature { Signature = new byte[] { 0x66, 0x74, 0x79, 0x70, 0x68, 0x65, 0x69, 0x63 }, Offset = 4, Extension = ".heic", MimeType = "image/heic" },
            new FileSignature { Signature = new byte[] { 0x66, 0x74, 0x79, 0x70, 0x68, 0x65, 0x69, 0x78 }, Offset = 4, Extension = ".heic", MimeType = "image/heic" },
        
            // AVIF
            new FileSignature { Signature = new byte[] { 0x66, 0x74, 0x79, 0x70, 0x61, 0x76, 0x69, 0x66 }, Offset = 4, Extension = ".avif", MimeType = "image/avif" },
        
            // VIDEO
            // MP4 - различные бренды
            new FileSignature { Signature = new byte[] { 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D }, Offset = 4, Extension = ".mp4", MimeType = "video/mp4" },
            new FileSignature { Signature = new byte[] { 0x66, 0x74, 0x79, 0x70, 0x6D, 0x70, 0x34, 0x31 }, Offset = 4, Extension = ".mp4", MimeType = "video/mp4" },
            new FileSignature { Signature = new byte[] { 0x66, 0x74, 0x79, 0x70, 0x6D, 0x70, 0x34, 0x32 }, Offset = 4, Extension = ".mp4", MimeType = "video/mp4" },
            new FileSignature { Signature = new byte[] { 0x66, 0x74, 0x79, 0x70, 0x61, 0x76, 0x63, 0x31 }, Offset = 4, Extension = ".mp4", MimeType = "video/mp4" },
            new FileSignature { Signature = new byte[] { 0x66, 0x74, 0x79, 0x70, 0x68, 0x65, 0x76, 0x31 }, Offset = 4, Extension = ".mp4", MimeType = "video/mp4" },
            new FileSignature { Signature = new byte[] { 0x66, 0x74, 0x79, 0x70, 0x64, 0x61, 0x73, 0x68 }, Offset = 4, Extension = ".mp4", MimeType = "video/mp4" },
        
            // MOV (QuickTime)
            new FileSignature { Signature = new byte[] { 0x66, 0x74, 0x79, 0x70, 0x71, 0x74, 0x20, 0x20 }, Offset = 4, Extension = ".mov", MimeType = "video/quicktime" },
            new FileSignature { Signature = new byte[] { 0x66, 0x74, 0x79, 0x70, 0x4D, 0x34, 0x56, 0x20 }, Offset = 4, Extension = ".mov", MimeType = "video/quicktime" },
            // Legacy MOV
            new FileSignature { Signature = new byte[] { 0x6D, 0x6F, 0x6F, 0x76 }, Offset = 4, Extension = ".mov", MimeType = "video/quicktime" },
            new FileSignature { Signature = new byte[] { 0x6D, 0x64, 0x61, 0x74 }, Offset = 4, Extension = ".mov", MimeType = "video/quicktime" },
        
            // AVI
            new FileSignature { Signature = new byte[] { 0x52, 0x49, 0x46, 0x46 }, Offset = 0, Extension = ".avi", MimeType = "video/x-msvideo",
                AdditionalCheck = buffer => buffer.Length >= 12 &&
                              buffer[8] == 0x41 && buffer[9] == 0x56 &&
                              buffer[10] == 0x49 && buffer[11] == 0x20
            },
        
            // MKV
            new FileSignature { Signature = new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }, Offset = 0, Extension = ".mkv", MimeType = "video/x-matroska",
                AdditionalCheck = buffer => CheckMatroskaDocType(buffer, "matroska")
            },
        
            // WebM
            new FileSignature { Signature = new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }, Offset = 0, Extension = ".webm", MimeType = "video/webm",
                AdditionalCheck = buffer => CheckMatroskaDocType(buffer, "webm")
            },
        
            // FLV
            new FileSignature { Signature = new byte[] { 0x46, 0x4C, 0x56 }, Offset = 0, Extension = ".flv", MimeType = "video/x-flv" },
        
            // WMV/ASF
            new FileSignature {
                Signature = new byte[] {
                0x30, 0x26, 0xB2, 0x75, 0x8E, 0x66, 0xCF, 0x11,
                0xA6, 0xD9, 0x00, 0xAA, 0x00, 0x62, 0xCE, 0x6C
                },
                Offset = 0, Extension = ".wmv", MimeType = "video/x-ms-wmv"
            },
        
            // MPEG - Elementary Stream
            new FileSignature { Signature = new byte[] { 0x00, 0x00, 0x01, 0xB3 }, Offset = 0, Extension = ".mpg", MimeType = "video/mpeg" },
            // MPEG - Program Stream
            new FileSignature { Signature = new byte[] { 0x00, 0x00, 0x01, 0xBA }, Offset = 0, Extension = ".mpg", MimeType = "video/mpeg" },
            // MPEG - Transport Stream
            new FileSignature { Signature = new byte[] { 0x47 }, Offset = 0, Extension = ".ts", MimeType = "video/mp2t",
                AdditionalCheck = buffer => CheckMpegTransportStream(buffer)
            },
        };

        public static ImageTypeInfo GetDataTypeInfo(Stream stream)
        {
            if (stream == null || !stream.CanRead) return _unknown;
            byte[] buffer = new byte[8192];
            long originalPosition = stream.Position;
            stream.Position = 0;
            try
            {
                int bytesRead = ReadFully(stream, buffer);
                if (bytesRead == 0)
                    return _unknown;
                if (bytesRead < buffer.Length)
                {
                    Array.Resize(ref buffer, bytesRead);
                }

                foreach (var sig in signatures)
                {
                    if (CheckSignature(buffer, bytesRead, sig))
                    {
                        return new ImageTypeInfo(sig.Extension, sig.MimeType);
                    }
                }
                return _unknown;
            }
            finally
            {
                if (stream.CanSeek)
                    stream.Position = originalPosition;
            }
        }

        private static int ReadFully(Stream stream, byte[] buffer)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int read = stream.Read(buffer, total, buffer.Length - total);
                if (read <= 0) break;
                total += read;
            }
            return total;
        }

        private static bool CheckSignature(byte[] buffer, int bufferLength, FileSignature signature)
        {
            if (bufferLength < signature.Offset + signature.Signature.Length)
                return false;

            for (int i = 0; i < signature.Signature.Length; i++)
            {
                if (buffer[signature.Offset + i] != signature.Signature[i])
                    return false;
            }

            if (signature.AdditionalCheck != null)
                return signature.AdditionalCheck(buffer);

            return true;
        }

        private static bool CheckMatroskaDocType(byte[] buffer, string expectedDocType)
        {
            string bufferAsString = Encoding.ASCII.GetString(buffer, 0, Math.Min(buffer.Length, 100));
            return bufferAsString.Contains(expectedDocType);
        }

        private static bool CheckMpegTransportStream(byte[] buffer)
        {
            if (buffer.Length < 376)
                return false;
            return buffer[0] == 0x47 && buffer[188] == 0x47;
        }

        // Дополнительные методы проверки для RAW форматов

        private static bool CheckDngFormat(byte[] buffer)
        {
            // DNG является TIFF-файлом, отличается наличием обязательного тега DNGVersion (0xC612) в IFD0.
            // Парсим IFD0 и ищем DNG-специфичные теги — это надёжнее любой эвристики по подстрокам.
            return TiffIfd0HasAnyTag(buffer, _dngTags);
        }

        // DNG-специфичные теги (по спецификации Adobe DNG):
        // 0xC612 DNGVersion (mandatory), 0xC613 DNGBackwardVersion, 0xC614 UniqueCameraModel,
        // 0xC621 ColorMatrix1, 0xC622 ColorMatrix2.
        private static readonly ushort[] _dngTags = new ushort[] { 0xC612, 0xC613, 0xC614, 0xC621, 0xC622 };

        /// <summary>
        /// Проверяет, содержит ли IFD0 (первая директория TIFF) хотя бы один из заданных тегов.
        /// </summary>
        private static bool TiffIfd0HasAnyTag(byte[] buffer, ushort[] tags)
        {
            if (buffer.Length < 8) return false;
            bool littleEndian;
            if (buffer[0] == 0x49 && buffer[1] == 0x49 && buffer[2] == 0x2A && buffer[3] == 0x00) littleEndian = true;
            else if (buffer[0] == 0x4D && buffer[1] == 0x4D && buffer[2] == 0x00 && buffer[3] == 0x2A) littleEndian = false;
            else return false;

            long ifd0Offset = ReadUInt32(buffer, 4, littleEndian);
            if (ifd0Offset < 8 || ifd0Offset + 2 > buffer.Length) return false;

            int numEntries = ReadUInt16(buffer, (int)ifd0Offset, littleEndian);
            long entriesEnd = ifd0Offset + 2 + (long)numEntries * 12;
            if (entriesEnd > buffer.Length) return false; // IFD0 не уместился в прочитанный буфер

            for (int i = 0; i < numEntries; i++)
            {
                int entryOffset = (int)ifd0Offset + 2 + i * 12;
                ushort tag = (ushort)ReadUInt16(buffer, entryOffset, littleEndian);
                for (int t = 0; t < tags.Length; t++)
                {
                    if (tag == tags[t]) return true;
                }
            }
            return false;
        }

        private static int ReadUInt16(byte[] buffer, int offset, bool littleEndian) =>
            littleEndian
                ? buffer[offset] | (buffer[offset + 1] << 8)
                : (buffer[offset] << 8) | buffer[offset + 1];

        private static long ReadUInt32(byte[] buffer, int offset, bool littleEndian) =>
            littleEndian
                ? (uint)(buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16) | (buffer[offset + 3] << 24))
                : (uint)((buffer[offset] << 24) | (buffer[offset + 1] << 16) | (buffer[offset + 2] << 8) | buffer[offset + 3]);

        private static bool CheckNefFormat(byte[] buffer)
        {
            // NEF файлы содержат строку "NIKON" в начале файла
            if (buffer.Length < 100) return false;

            string bufferAsString = Encoding.ASCII.GetString(buffer, 0, Math.Min(buffer.Length, 512));
            return bufferAsString.Contains("NIKON") || bufferAsString.Contains("COOLPIX");
        }

        private static bool CheckArwFormat(byte[] buffer)
        {
            // ARW файлы содержат информацию о Sony в метаданных
            if (buffer.Length < 100) return false;

            string bufferAsString = Encoding.ASCII.GetString(buffer, 0, Math.Min(buffer.Length, 512));
            return bufferAsString.Contains("SONY") || bufferAsString.Contains("ARW");
        }

        private static bool CheckSr2Format(byte[] buffer)
        {
            // SR2 файлы содержат информацию о Sony и специфичные структуры SR2
            if (buffer.Length < 100) return false;

            string bufferAsString = Encoding.ASCII.GetString(buffer, 0, Math.Min(buffer.Length, 512));
            return bufferAsString.Contains("SONY") &&
                   (bufferAsString.Contains("SR2") || bufferAsString.Contains("DSC-R1"));
        }

        private static bool CheckSrfFormat(byte[] buffer)
        {
            // SRF файлы содержат информацию о Sony и специфичные структуры SRF
            if (buffer.Length < 100) return false;

            string bufferAsString = Encoding.ASCII.GetString(buffer, 0, Math.Min(buffer.Length, 512));
            return bufferAsString.Contains("SONY") &&
                   (bufferAsString.Contains("SRF") || bufferAsString.Contains("DSC-F828"));
        }
    }
}