using System;

namespace BlazorApp.Shared
{
    public class EloModel
    {
        public string PicId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PictureUri { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; }
    }
}
