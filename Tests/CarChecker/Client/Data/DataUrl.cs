using System;
using System.IO;

namespace CarChecker.Client.Data
{
    public static class DataUrl
    {
        public static string ToDataUrl(this MemoryStream data, string format)
        {
            Span<byte> span = new Span<byte>(data.GetBuffer()).Slice(0, (int)data.Length);
            return $"data:{format};base64,{Convert.ToBase64String(span)}";
        }

        public static byte[] ToBytes(string url)
        {
            int commaPos = url.IndexOf(',');
            if (commaPos >= 0)
            {
                string base64 = url.Substring(commaPos + 1);
                return Convert.FromBase64String(base64);
            }

            return null;
        }
    }
}
