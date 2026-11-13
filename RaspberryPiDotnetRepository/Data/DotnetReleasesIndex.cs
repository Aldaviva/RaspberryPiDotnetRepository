using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace RaspberryPiDotnetRepository.Data;

[JsonSchemaTypeGenerator("dotnet-releases-index.json")]
public readonly partial struct DotnetReleasesIndex: IDisposable {

    public void Dispose() {
        ((IJsonElement) this).ParentDocument.Dispose();
    }

}