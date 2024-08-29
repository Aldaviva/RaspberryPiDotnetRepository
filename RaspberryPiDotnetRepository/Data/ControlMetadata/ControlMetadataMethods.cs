namespace RaspberryPiDotnetRepository.Data.ControlMetadata;

public static class ControlMetadataMethods {

    public static string serialize(this Section  section) => section.ToString().ToLowerInvariant().Replace('_', '-');
    public static string serialize(this Priority priority) => priority.ToString().ToLowerInvariant();

    public static string serialize(this Inequality inequality) => inequality switch {
        Inequality.EQUAL                    => "=",
        Inequality.LESS_THAN                => "<<",
        Inequality.LESS_THAN_OR_EQUAL_TO    => "<=",
        Inequality.GREATER_THAN             => ">>",
        Inequality.GREATER_THAN_OR_EQUAL_TO => ">="
    };

    public static IEnumerable<string> serializeAll<T>(this IEnumerable<T> serializables) where T: DebianSerializable {
        return serializables.Select(serializable => serializable.serialize());
    }

}