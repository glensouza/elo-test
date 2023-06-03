using System;
using System.Collections.Generic;
using System.Text;

namespace BlazorApp.Shared
{
    public class EloVoteModel
    {
        public string PicId1 { get; set; }
        public string Name1 { get; set; } = string.Empty;
        public string PictureUri1 { get; set; } = string.Empty;
        public string PicId2 { get; set; }
        public string Name2 { get; set; } = string.Empty;
        public string PictureUri2 { get; set; } = string.Empty;
    }
}
