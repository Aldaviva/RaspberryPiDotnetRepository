using System.Text.Json.Serialization;

namespace RaspberryPiDotnetRepository.Data;

public readonly record struct FileCacheState(DateTimeOffset? lastModified, string? etag);

public interface CachedBlob {

    [JsonIgnore]
    public FileCacheState? cacheState { get; set; }

}