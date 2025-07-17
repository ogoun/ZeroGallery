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
            
            // Sony ARW - использует TIFF заголовок с проверкой
            new FileSignature {
                Signature = new byte[] { 0x49, 0x49, 0x2A, 0x00 },
                Offset = 0,
                Extension = ".arw",
                MimeType = "image/x-sony-arw",
                AdditionalCheck = buffer => CheckArwFormat(buffer)
            },

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
            // SVG - direct tag
            new FileSignature { Signature = new byte[] { 0x3C, 0x73, 0x76, 0x67, 0x20 }, Offset = 0, Extension = ".svg", MimeType = "image/svg+xml" },
        
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
            byte[] buffer = new byte[512];
            long originalPosition = stream.Position;
            stream.Position = 0;
            try
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                    return _unknown;

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
            // DNG файлы являются TIFF файлами с специфическими тегами Adobe
            // Для полной проверки потребовалось бы парсить IFD структуру TIFF
            // Здесь используем упрощённую проверку по содержимому
            if (buffer.Length < 100) return false;

            string bufferAsString = Encoding.ASCII.GetString(buffer, 0, Math.Min(buffer.Length, 512));
            return bufferAsString.Contains("Adobe") || bufferAsString.Contains("DNG");
        }

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