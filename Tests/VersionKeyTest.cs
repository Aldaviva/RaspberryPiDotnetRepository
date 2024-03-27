using RaspberryPiDotnetRepository.Data;
using System.Text.Json;

namespace Tests;

public class VersionKeyTest {

    [Fact]
    public void equal() {
        VersionKey a = new(new SortedSet<string> { "6.0.28", "7.0.17", "8.0.3" }, new SortedSet<int> { 10, 11, 12 });
        VersionKey b = new(new SortedSet<string> { "6.0.28", "7.0.17", "8.0.3" }, new SortedSet<int> { 10, 11, 12 });
        a.Should().Be(b);
        a.Equals(b).Should().BeTrue("Equals method");
    }

    [Fact]
    public void inequal() {
        VersionKey a = new(new SortedSet<string> { "6.0.28", "7.0.17", "8.0.3" }, new SortedSet<int> { 10, 11, 12 });
        VersionKey b = new(new SortedSet<string> { "6.0.28", "7.0.17", "8.0.2" }, new SortedSet<int> { 10, 11, 12 });
        a.Should().NotBe(b);
        a.Equals(b).Should().BeFalse("Equals method");
    }

    [Fact]
    public void serialize() {
        VersionKey   deserialized = new(new SortedSet<string> { "6.0.28", "7.0.17", "8.0.3" }, new SortedSet<int> { 10, 11, 12 });
        string       actual       = JsonSerializer.Serialize(deserialized);
        const string EXPECTED     = """{"dotnetVersions":["6.0.28","7.0.17","8.0.3"],"debianVersions":[10,11,12]}""";

        actual.Should().Be(EXPECTED);
    }

    [Fact]
    public void deserialize() {
        const string SERIALIZED = """{"dotnetVersions":["6.0.28","7.0.17","8.0.3"],"debianVersions":[10,11,12]}""";
        VersionKey   actual     = JsonSerializer.Deserialize<VersionKey>(SERIALIZED)!;
        VersionKey   expected   = new(new SortedSet<string> { "6.0.28", "7.0.17", "8.0.3" }, new SortedSet<int> { 10, 11, 12 });

        actual.Should().Be(expected);
    }

}