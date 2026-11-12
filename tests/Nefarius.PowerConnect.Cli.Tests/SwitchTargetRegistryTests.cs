using System.Text.Json;

using Nefarius.PowerConnect.Cli.Mcp;

namespace Nefarius.PowerConnect.Cli.Tests;

public sealed class SwitchTargetRegistryTests : IDisposable
{
    private readonly string _directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "pc28xx-" + Guid.NewGuid().ToString("N"))).FullName;
    private const string Secret = "super-secret-password";

    [Fact]
    public void LoadsAliasesWithoutCopyingPasswordsIntoTheRegistry()
    {
        Dictionary<string, string> environment = new Dictionary<string, string>(StringComparer.Ordinal);
        SwitchTargetRegistry registry = SwitchTargetRegistry.Load(Write(ValidTargets()), name => environment.GetValueOrDefault(name));

        SwitchTargetInfo info = Assert.Single(registry.List());
        Assert.Equal("core", info.Alias);
        Assert.Equal("192.168.2.17", info.Host);
        Assert.Equal("admin", info.User);
        Assert.Equal(30, info.TimeoutSeconds);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(registry.List()));

        environment["PC28XX_CORE_PASSWORD"] = Secret;
        Assert.Equal(Secret, registry.CreateConnection(registry.Get("CORE")).Password);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(registry.List()));
    }

    [Fact]
    public void RejectsAPasswordEmbeddedInTheFile()
    {
        string json = """
            {
              "switches": [
                {
                  "alias": "core",
                  "host": "192.168.2.17",
                  "password": "super-secret-password",
                  "passwordEnvironmentVariable": "PC28XX_CORE_PASSWORD"
                }
              ]
            }
            """;

        SwitchTargetException exception = Assert.Throws<SwitchTargetException>(() => SwitchTargetRegistry.Load(Write(json)));
        Assert.Contains("must not contain passwords", exception.Message);
        Assert.DoesNotContain(Secret, exception.Message);
    }

    [Fact]
    public void RejectsDuplicateAliasesAndCredentialBearingHosts()
    {
        Assert.Contains("more than once", Assert.Throws<SwitchTargetException>(() => SwitchTargetRegistry.Load(Write("""
            {
              "switches": [
                { "alias": "core", "host": "192.168.2.17", "passwordEnvironmentVariable": "PC28XX_CORE_PASSWORD" },
                { "alias": "Core", "host": "192.168.2.18", "passwordEnvironmentVariable": "PC28XX_EDGE_PASSWORD" }
              ]
            }
            """))).Message);

        SwitchTargetException host = Assert.Throws<SwitchTargetException>(() => SwitchTargetRegistry.Load(Write("""
            {
              "switches": [
                { "alias": "core", "host": "http://admin:super-secret-password@192.168.2.17", "passwordEnvironmentVariable": "PC28XX_CORE_PASSWORD" }
              ]
            }
            """)));
        Assert.Contains("credentials", host.Message);
        Assert.DoesNotContain(Secret, host.Message);
    }

    [Fact]
    public void ReportsAMissingAliasOrPasswordWithoutEchoingSecrets()
    {
        Dictionary<string, string> environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PC28XX_OTHER_PASSWORD"] = Secret
        };
        SwitchTargetRegistry registry = SwitchTargetRegistry.Load(Write(ValidTargets()), name => environment.GetValueOrDefault(name));

        SwitchTargetException unknown = Assert.Throws<SwitchTargetException>(() => registry.Get("lab"));
        Assert.Contains("Unknown switch 'lab'", unknown.Message);
        Assert.Contains("core", unknown.Message);
        Assert.DoesNotContain(Secret, unknown.Message);

        SwitchTargetException missing = Assert.Throws<SwitchTargetException>(() => registry.CreateConnection(registry.Get("core")));
        Assert.Contains("PC28XX_CORE_PASSWORD", missing.Message);
        Assert.DoesNotContain(Secret, missing.Message);
    }

    [Theory]
    [InlineData("""{ "switches": [] }""", "at least one")]
    [InlineData("""{ "switches": [ { "alias": "core", "host": "192.168.2.17" } ] }""", "passwordEnvironmentVariable")]
    [InlineData("""{ "switches": [ { "alias": "core", "host": "192.168.2.17", "passwordEnvironmentVariable": "not a var" } ] }""", "environment variable name")]
    public void RejectsInvalidTargetFiles(string json, string message)
    {
        SwitchTargetException exception = Assert.Throws<SwitchTargetException>(() => SwitchTargetRegistry.Load(Write(json)));
        Assert.Contains(message, exception.Message);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private string Write(string json)
    {
        string path = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        return path;
    }

    private static string ValidTargets()
    {
        return """
            {
              "switches": [
                {
                  "alias": "core",
                  "host": "192.168.2.17",
                  "passwordEnvironmentVariable": "PC28XX_CORE_PASSWORD"
                }
              ]
            }
            """;
    }
}
