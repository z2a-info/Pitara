using System;

namespace CommonProject.Src
{
    public class PhotoMeta
    {
        public string CustomeKeyWords { get; set; } = string.Empty;
        public string ThumbNail { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public string CameraMake { get; set; } = string.Empty;
        public string CameraModel{ get; set; } = string.Empty;
        public DateTime? DateTaken { get; set; }
        public double Longitude { get; set; } = 0;
        public double Lattitude { get; set; } = 0;
        public double Altitude { get; set; } = 0;
    }
}
