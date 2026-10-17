using Nefarius.PowerConnect.Ews.Changes;
using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Http;

namespace Nefarius.PowerConnect.Ews.Files;

/// <summary>TFTP file copy and factory-default restore.</summary>
public sealed class FileRepository
{
    private readonly SwitchClient _client;

    /// <summary>Creates a repository bound to a session.</summary>
    public FileRepository(SwitchClient client)
    {
        _client = client;
    }

    /// <summary>
    ///     Plans a TFTP download onto the switch. Firmware is file type 8, boot is 9, config is 6.
    ///     Firmware and boot are destructive.
    /// </summary>
    public async Task<ChangePlan> PlanDownloadAsync(
        string tftpHost,
        string fileName,
        string type,
        CancellationToken cancellationToken = default)
    {
        (string fileType, bool destructive) = MapDownload(type);
        FormPage page = await _client.GetFormAsync("/config/software_dl.htm", cancellationToken).ConfigureAwait(false);
        string history = page.ValueOf("rlCopyFreeHistoryIndex$scalar") ?? "1";
        Dictionary<string, string> mutations = CopyMutations(
            sourceLocation: "3",
            sourceAddress: tftpHost,
            sourceFile: fileName,
            sourceType: null,
            destinationLocation: "1",
            destinationAddress: null,
            destinationFile: null,
            destinationType: fileType,
            history: history);
        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(
            _client,
            "Download " + type + " from " + tftpHost + ":" + fileName,
            page,
            mutations,
            destructive: destructive));
        return plan;
    }

    /// <summary>
    ///     Plans a TFTP upload of the configuration (file type 6). Backup uses the same file type; the
    ///     destination name distinguishes the copy.
    /// </summary>
    public async Task<ChangePlan> PlanUploadAsync(
        string tftpHost,
        string fileName,
        string type,
        CancellationToken cancellationToken = default)
    {
        if (type.Trim().ToLowerInvariant() is not ("config" or "backup"))
        {
            throw new FormatException("Upload type must be config or backup.");
        }

        FormPage page = await _client.GetFormAsync("/config/software_ul.htm", cancellationToken).ConfigureAwait(false);
        string history = page.ValueOf("rlCopyFreeHistoryIndex$scalar") ?? "1";
        Dictionary<string, string> mutations = CopyMutations(
            sourceLocation: "1",
            sourceAddress: null,
            sourceFile: null,
            sourceType: "6",
            destinationLocation: "3",
            destinationAddress: tftpHost,
            destinationFile: fileName,
            destinationType: null,
            history: history);
        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(_client, "Upload " + type + " to " + tftpHost + ":" + fileName, page, mutations));
        return plan;
    }

    /// <summary>Plans restore of configuration factory defaults (rndAction 20). Destructive.</summary>
    public async Task<ChangePlan> PlanRestoreDefaultsAsync(CancellationToken cancellationToken = default)
    {
        FormPage page = await _client.GetFormAsync("/config/software_active.htm", cancellationToken).ConfigureAwait(false);
        ChangePlan plan = new ChangePlan();
        plan.Add(FormChange.Post(
            _client,
            "Restore configuration factory defaults",
            page,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["rndAction"] = "20"
            },
            destructive: true));
        return plan;
    }

    private static (string FileType, bool Destructive) MapDownload(string type)
    {
        return type.Trim().ToLowerInvariant() switch
        {
            "firmware" => ("8", true),
            "boot" => ("9", true),
            "config" => ("6", false),
            _ => throw new FormatException("Download type must be firmware, boot, or config.")
        };
    }

    private static Dictionary<string, string> CopyMutations(
        string sourceLocation,
        string? sourceAddress,
        string? sourceFile,
        string? sourceType,
        string destinationLocation,
        string? destinationAddress,
        string? destinationFile,
        string? destinationType,
        string history)
    {
        Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["rlCopyIndex$Add"] = "1",
            ["rlCopySourceLocation$Add"] = sourceLocation,
            ["rlCopyDestinationLocation$Add"] = destinationLocation,
            ["rlCopyRowStatus$Add"] = "4",
            ["rlCopyHistoryIndex$Add"] = history
        };
        if (sourceAddress != null)
        {
            mutations["rlCopySourceIpAddress$Add"] = sourceAddress;
        }

        if (sourceFile != null)
        {
            mutations["rlCopySourceFileName$Add"] = sourceFile;
        }

        if (sourceType != null)
        {
            mutations["rlCopySourceFileType$Add"] = sourceType;
        }

        if (destinationAddress != null)
        {
            mutations["rlCopyDestinationIpAddress$Add"] = destinationAddress;
        }

        if (destinationFile != null)
        {
            mutations["rlCopyDestinationFileName$Add"] = destinationFile;
        }

        if (destinationType != null)
        {
            mutations["rlCopyDestinationFileType$Add"] = destinationType;
        }

        return mutations;
    }
}
