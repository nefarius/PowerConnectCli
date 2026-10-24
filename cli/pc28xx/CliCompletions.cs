using System.CommandLine;
using System.Globalization;

using Nefarius.PowerConnect.Ews.Ews;

namespace Nefarius.PowerConnect.Cli;

/// <summary>Static value suggestions for dotnet-suggest. Suggestions do not restrict accepted input.</summary>
internal static class CliCompletions
{
    private static readonly string[] PortNames = Names("g", PortName.PortCount);

    private static readonly string[] LagNames = Names("ch", PortName.LagCount);

    private static readonly string[] PortAndLagNames = PortNames.Concat(LagNames).ToArray();

    public static void OnOff(params Option[] options)
    {
        Values(["on", "off"], options);
    }

    public static void OnOff(Argument argument)
    {
        argument.CompletionSources.Add("on", "off");
    }

    public static void Values(IReadOnlyList<string> values, params Option[] options)
    {
        string[] choices = values.ToArray();
        foreach (Option option in options)
        {
            option.CompletionSources.Add(choices);
        }
    }

    public static void Values(IReadOnlyList<string> values, Argument argument)
    {
        argument.CompletionSources.Add(values.ToArray());
    }

    public static void Ports(Argument argument)
    {
        argument.CompletionSources.Add(PortAndLagNames);
    }

    public static void Ports(params Option[] options)
    {
        foreach (Option option in options)
        {
            option.CompletionSources.Add(PortAndLagNames);
        }
    }

    public static void Lags(Argument argument)
    {
        argument.CompletionSources.Add(LagNames);
    }

    public static void PortList(Argument argument)
    {
        argument.CompletionSources.Add(context => CompletePortList(context.WordToComplete));
    }

    public static void PortList(params Option[] options)
    {
        foreach (Option option in options)
        {
            option.CompletionSources.Add(context => CompletePortList(context.WordToComplete));
        }
    }

    private static IEnumerable<string> CompletePortList(string word)
    {
        int split = word.LastIndexOf(',');
        string prefix = split < 0 ? "" : word[..(split + 1)];
        string current = split < 0 ? word : word[(split + 1)..];
        HashSet<string> selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (split > 0)
        {
            foreach (string part in word[..split].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                selected.Add(part);
            }
        }

        foreach (string name in PortAndLagNames)
        {
            if (selected.Contains(name))
            {
                continue;
            }

            if (current.Length > 0 && !name.StartsWith(current, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return prefix + name;
        }
    }

    private static string[] Names(string prefix, int count)
    {
        string[] names = new string[count];
        for (int index = 1; index <= count; index++)
        {
            names[index - 1] = prefix + index.ToString(CultureInfo.InvariantCulture);
        }

        return names;
    }
}
