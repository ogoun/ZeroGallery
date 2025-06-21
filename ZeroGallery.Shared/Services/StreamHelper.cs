namespace ZeroGallery.Shared.Services
{
    internal static class StreamHelper
    {
        /// <summary>
        /// Размер буфера при переносе данных из потока в поток
        /// </summary>
        private const int DEFAULT_STREAM_BUFFER_SIZE = 16384;

        /// <summary>
        /// Копирование данных из потока в поток
        /// </summary>
        internal static async Task<long> Transfer(Stream input, Stream output)
        {
            if (input.CanRead == false)
            {
                throw new InvalidOperationException("Input stream can not be read.");
            }
            if (output.CanWrite == false)
            {
                throw new InvalidOperationException("Output stream can not be write.");
            }
            long totalBytes = 0;
            var readed = 0;
            var buffer = new byte[DEFAULT_STREAM_BUFFER_SIZE];
            while ((readed = input.Read(buffer, 0, buffer.Length)) != 0)
            {
                await output.WriteAsync(buffer, 0, readed);
                totalBytes += readed;
            }
            await output.FlushAsync();
            return readed;
        }
    }
}
