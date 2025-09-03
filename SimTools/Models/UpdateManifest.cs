using System;
using System.Text.Json.Serialization;

namespace SimTools.Models
{
    public sealed class UpdateManifest
    {
        [JsonPropertyName("version")]
        public string VersionString { get; set; } = "0.0.0.0";

        [JsonIgnore]
        public Version Version => Version.TryParse(VersionString, out var v) ? v : new Version(0, 0, 0, 0);

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty; // direct link to MSI/MSIX/EXE

        [JsonPropertyName("sha256")]
        public string? Sha256 { get; set; } // optional integrity check (hex)

        [JsonPropertyName("releaseNotes")]
        public string? ReleaseNotes { get; set; }

        [JsonPropertyName("mandatory")]
        public bool Mandatory { get; set; }
    }
}
