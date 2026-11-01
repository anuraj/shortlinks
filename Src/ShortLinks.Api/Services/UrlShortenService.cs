using System.Security.Cryptography;
using System.Text;

namespace ShortLinks.Api.Services
{
    public class UrlShortenService
    {
        public async Task<string> ShortenUrl(string longUrl, CancellationToken cancellationToken = default)
        {
            var fixedLongUrl = longUrl.Trim();
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(fixedLongUrl));
            var key = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
            var hash = await HMACSHA256.HashDataAsync(key, stream, cancellationToken);
            var value = (uint)((hash[0] << 16) | (hash[1] << 8) | hash[2]);
            const string base62Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            var result = new StringBuilder();

            while (value > 0)
            {
                result.Insert(0, base62Chars[(int)(value % 62)]);
                value /= 62;
            }

            return result.Length == 0 ? "0" : result.ToString();
        }
    }
}