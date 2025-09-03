using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SimTools.Models;

namespace SimTools.Services
{
    public sealed class UpdateService
    {
        private static readonly HttpClient _http = new(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        })
        { Timeout = TimeSpan.FromSeconds(20) };

        public async Task<UpdateManifest> FetchManifestAsync(string url, CancellationToken ct)
        {
            using var res = await _http.GetAsync(url, ct);
            res.EnsureSuccessStatusCode();
            await using var s = await res.Content.ReadAsStreamAsync(ct);
            var manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(s, cancellationToken: ct)
                           ?? throw new InvalidOperationException("Invalid update manifest.");
            if(string.IsNullOrWhiteSpace(manifest.Url))
                throw new InvalidOperationException("Manifest missing 'url'.");
            return manifest;
        }

        public async Task<string> DownloadAsync(string url, string? expectedSha256Hex, Action<double> progress, CancellationToken ct)
        {
            using var res = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            res.EnsureSuccessStatusCode();

            var total = res.Content.Headers.ContentLength;
            var fileName = Path.Combine(Path.GetTempPath(), Path.GetFileName(new Uri(url).LocalPath));

            await using var httpStream = await res.Content.ReadAsStreamAsync(ct);
            await using var fs = File.Create(fileName);

            var buffer = new byte[81920];
            long readTotal = 0;
            int read;

            using var hasher = SHA256.Create();
            while((read = await httpStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
            {
                await fs.WriteAsync(buffer.AsMemory(0, read), ct);
                hasher.TransformBlock(buffer, 0, read, null, 0);
                readTotal += read;
                if(total.HasValue)
                    progress(Math.Clamp((double)readTotal / total.Value, 0, 1));
            }
            hasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            if(!string.IsNullOrWhiteSpace(expectedSha256Hex))
            {
                var actualHex = Convert.ToHexString(hasher.Hash!).ToLowerInvariant();
                if(!string.Equals(actualHex, expectedSha256Hex.Trim().ToLowerInvariant(), StringComparison.Ordinal))
                {
                    try { File.Delete(fileName); } catch { }
                    throw new InvalidOperationException($"Download integrity check failed. Expected {expectedSha256Hex}, got {actualHex}.");
                }
            }

            return fileName;
        }

        public void RunInstaller(string filePath)
        {
            if(!File.Exists(filePath))
                throw new FileNotFoundException(filePath);

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var psi = new ProcessStartInfo { FileName = filePath, UseShellExecute = true, Verb = "open" };

            switch(ext)
            {
                case ".msix":
                case ".msixbundle":
                case ".appx":
                    Process.Start(psi); // Shell handles MSIX/App Installer UX
                    break;
                case ".msi":
                    psi.FileName = "msiexec";
                    psi.Arguments = $"/i \"{filePath}\"";
                    Process.Start(psi);
                    break;
                case ".exe":
                    Process.Start(psi);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported installer type: {ext}");
            }
        }
    }
}
