namespace SamErpBackend.Models
{
    public class AppSettingsModel
    {
        public string server        { get; set; } = string.Empty;
        public string db            { get; set; } = string.Empty;
        public string user          { get; set; } = string.Empty;
        public string password      { get; set; } = string.Empty;
        //public string AllowedOrigin { get; set; } = "http://localhost:8001";
    }
}
