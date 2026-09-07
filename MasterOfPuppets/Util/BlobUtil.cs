using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace MasterOfPuppets.Util
{
    /// <summary>
    /// Utility class for encoding and decoding blobs used by the plugin.
    /// Provides line‑wrapped Base64 output and automatic GZip detection on decode.
    /// </summary>
    public static class BlobUtil
    {
        /// <summary>
        /// Encode raw bytes to a Base64 string. Optionally compress the data using GZip.
        /// The Base64 output is formatted with line breaks every 76 characters (MIME format).
        /// </summary>
        /// <param name="data">The raw byte array to encode.</param>
        /// <param name="compress">If true, data is compressed with GZip before encoding.</param>
        /// <returns>Line‑wrapped Base64 string.</returns>
        public static string Encode(byte[] data, bool compress = true)
        {
            if (compress)
            {
                using var output = new MemoryStream();
                using (var gzip = new GZipStream(output, CompressionMode.Compress))
                {
                    gzip.Write(data, 0, data.Length);
                }
                data = output.ToArray();
            }
            return Convert.ToBase64String(data, Base64FormattingOptions.InsertLineBreaks);
        }

        /// <summary>
        /// Decode a Base64 blob string, automatically handling optional line breaks and detecting GZip compression.
        /// Returns the original UTF‑8 decoded string.
        /// </summary>
        /// <param name="blob">The Base64 encoded string, optionally with line breaks.</param>
        /// <returns>Decoded UTF‑8 string.</returns>
        public static string Decode(string blob)
        {
            if (string.IsNullOrWhiteSpace(blob))
                return string.Empty;

            // Remove any whitespace or line breaks to obtain a clean Base64 string.
            var cleaned = string.Concat(blob.Where(c => !char.IsWhiteSpace(c)));
            var data = Convert.FromBase64String(cleaned);

            // GZip files start with the magic numbers 0x1F, 0x8B.
            if (data.Length >= 2 && data[0] == 0x1F && data[1] == 0x8B)
            {
                using var input = new MemoryStream(data);
                using var gzip = new GZipStream(input, CompressionMode.Decompress);
                using var reader = new StreamReader(gzip, Encoding.UTF8);
                return reader.ReadToEnd();
            }
            // No compression; interpret bytes directly as UTF‑8 string.
            return Encoding.UTF8.GetString(data);
        }
    }
}
