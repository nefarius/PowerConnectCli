using Spectre.Console;

namespace Nefarius.PowerConnect.Cli;

/// <summary>Shows a transient spinner on standard error while a switch request is in flight.</summary>
internal static class CliActivity
{
    private static readonly IAnsiConsole ErrorConsole = AnsiConsole.Create(new AnsiConsoleSettings
    {
        Out = new AnsiConsoleOutput(Console.Error)
    });

    /// <summary>
    ///     True when a spinner can be shown without mixing into JSON, logs, or a redirected stream.
    /// </summary>
    public static bool IsEnabled(CliSession session)
    {
        return !session.Json &&
               !session.Verbose &&
               !Console.IsErrorRedirected &&
               ErrorConsole.Profile.Capabilities.Interactive &&
               ErrorConsole.Profile.Capabilities.Ansi;
    }

    /// <summary>Runs <paramref name="action"/>, with <paramref name="status"/> while it is in flight.</summary>
    public static async Task<T> RunAsync<T>(CliSession session, string status, Func<Task<T>> action)
    {
        if (!IsEnabled(session))
        {
            return await action().ConfigureAwait(false);
        }

        return await ErrorConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(new Style(Color.Grey))
            .StartAsync(Markup.Escape(status), _ => action())
            .ConfigureAwait(false);
    }
}
