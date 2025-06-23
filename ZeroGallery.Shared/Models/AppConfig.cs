namespace ZeroGalleryApp
{
    public class AppConfig
    {
        public string api_write_token { get; set; }
        public string data_folder { get; set; }
        public string api_master_token { get; set; }
        public string db_path { get; set; }
        public int port { get; set; } = -1;
        public bool convert_video_to_mp4 { get; set; } = true;
        public bool convert_heic_to_jpg { get; set; } = false;
        public bool convert_tiff_to_jpg { get; set; } = false;

        public bool convert_dng_to_jpg { get; set; } = true;
        public bool convert_cr2_to_jpg { get; set; } = true;
        public bool convert_nef_to_jpg { get; set; } = true;
        public bool convert_arw_to_jpg { get; set; } = true;
        public bool convert_orf_to_jpg { get; set; } = true;
    }
}
