using System.Diagnostics;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

using Nefarius.PowerConnect.Cli.Mcp;

namespace Nefarius.PowerConnect.Cli.Tests;

public sealed class McpStdioSmokeTests
{
    private const string Secret = "super-secret-password";

    [Fact]
    public void HelpListsTheMcpCommand()
    {
        ProcessResult result = Run("--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("mcp", result.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingTargetsFileFailsOnStderr()
    {
        ProcessResult result = Run("mcp");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal("", result.Stdout);
        Assert.Contains(McpCommand.TargetsFileVariable, result.Stderr);
    }

    [Fact]
    public async Task InitializeAndListToolsDoNotContactASwitch()
    {
        string directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "pc28xx-" + Guid.NewGuid().ToString("N"))).FullName;
        string targets = Path.Combine(directory, "switches.json");
        await File.WriteAllTextAsync(targets, """
            {
              "switches": [
                {
                  "alias": "core",
                  "host": "192.0.2.17",
                  "user": "admin",
                  "passwordEnvironmentVariable": "PC28XX_CORE_PASSWORD"
                }
              ]
            }
            """);

        StdioClientTransport transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Command = "dotnet",
            Arguments = ["exec", CliDll(), "mcp", "--targets-file", targets],
            Name = "pc28xx",
            EnvironmentVariables = new Dictionary<string, string?>
            {
                ["PC28XX_CORE_PASSWORD"] = Secret
            }
        });

        try
        {
            await using McpClient client = await McpClient.CreateAsync(transport, loggerFactory: NullLoggerFactory.Instance);
            IList<McpClientTool> tools = await client.ListToolsAsync();
            Assert.Equal(
                new[]
                {
                    "list_switches",
                    "port_list",
                    "port_set",
                    "port_show",
                    "raw_get",
                    "raw_set",
                    "system_asset",
                    "system_jumbo",
                    "system_reset",
                    "system_secure_mode",
                    "system_set_asset_tag",
                    "system_set_jumbo",
                    "system_set_secure_mode",
                    "system_versions",
                    "vlan_access",
                    "vlan_create",
                    "vlan_delete",
                    "vlan_list",
                    "vlan_port_list",
                    "vlan_port_set",
                    "vlan_port_show",
                    "vlan_set",
                    "vlan_show"
                },
                tools.Select(tool => tool.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());

            McpClientTool create = tools.Single(tool => tool.Name == "vlan_create");
            string schema = create.JsonSchema.ToString();
            Assert.Contains("\"switch\"", schema, StringComparison.Ordinal);
            Assert.Contains("\"apply\"", schema, StringComparison.Ordinal);
            using JsonDocument document = JsonDocument.Parse(schema);
            string[] required = document.RootElement.TryGetProperty("required", out JsonElement requiredElement)
                ? requiredElement.EnumerateArray().Select(item => item.GetString()!).ToArray()
                : Array.Empty<string>();
            Assert.Contains("switch", required);
            Assert.DoesNotContain("apply", required);

            McpClientTool raw = tools.Single(tool => tool.Name == "raw_set");
            Assert.Contains("\"i_understand\"", raw.JsonSchema.ToString(), StringComparison.Ordinal);

            CallToolResult listed = await client.CallToolAsync("list_switches", new Dictionary<string, object?>());
            string payload = JsonSerializer.Serialize(listed);
            Assert.True(listed.IsError != true);
            Assert.Contains("core", payload, StringComparison.Ordinal);
            Assert.Contains("192.0.2.17", payload, StringComparison.Ordinal);
            Assert.DoesNotContain(Secret, payload, StringComparison.Ordinal);
            Assert.DoesNotContain("PC28XX_CORE_PASSWORD", payload, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ProcessResult Run(params string[] args)
    {
        ProcessStartInfo start = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add("exec");
        start.ArgumentList.Add(CliDll());
        foreach (string arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        start.Environment.Remove(McpCommand.TargetsFileVariable);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("dotnet did not start.");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(20000), "pc28xx did not exit.");
        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    private static string CliDll()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "PowerConnectCli.sln")))
        {
            directory = directory.Parent;
        }

        if (directory == null)
        {
            throw new InvalidOperationException("Repository root was not found.");
        }

        string configuration = AppContext.BaseDirectory.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            ? "Release"
            : "Debug";
        string path = Path.Combine(directory.FullName, "cli", "pc28xx", "bin", configuration, "net10.0", "pc28xx.dll");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException("pc28xx was not built at " + path);
        }

        return path;
    }

    private sealed record ProcessResult(int ExitCode, string Stdout, string Stderr);
}
