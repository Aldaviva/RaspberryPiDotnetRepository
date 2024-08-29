namespace RaspberryPiDotnetRepository.Data.ControlMetadata;

public record PersonWithEmail(string name, string emailAddress): DebianSerializable {

    public string serialize() => $"{name} <{emailAddress}>";

}