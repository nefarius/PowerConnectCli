using Nefarius.PowerConnect.Ews.Http;

namespace Nefarius.PowerConnect.Cli.Mcp;

/// <summary>Turns switch failures into messages that are safe to return to an MCP client.</summary>
internal static class McpErrors
{
    public static bool TryDescribe(Exception exception, out string message)
    {
        switch (exception)
        {
            case SwitchTargetException:
            case FormatException:
            case SwitchException:
            case HttpRequestException:
                message = exception.Message;
                return true;
            case TaskCanceledException:
                message = "The switch did not respond in time.";
                return true;
            default:
                message = "The switch operation failed.";
                return exception is not OperationCanceledException;
        }
    }
}
