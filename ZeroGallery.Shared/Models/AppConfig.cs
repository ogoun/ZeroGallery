namespace ZeroGalleryApp
{
    public class AppConfig
    {
        public string api_write_token { get; set; }
        public string data_folder { get; set; }
        public string api_master_token { get; set; }
        public string db_path { get; set; }
        public int port { get; set; } = -1;
    }
}
