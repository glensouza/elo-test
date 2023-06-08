using System;

namespace BlazorApp.Shared
{
    public class EloModel
    {
        public string PicId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string PictureUri { get; set; } = string.Empty;
        public double Rating { get; set; } = 1200;
        public DateTime LastUpdated { get; set; }
    }
}
