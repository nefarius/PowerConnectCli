using System.Text.Json;

using Spectre.Console;

namespace Nefarius.PowerConnect.Cli;

/// <summary>Prints dictionaries and rows as a table or as JSON.</summary>
internal static class TextOutput
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

    public static void Write(object? value, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
            return;
        }

        if (value is IReadOnlyDictionary<string, string> map)
        {
            Table table = new Table();
            table.AddColumn("Field");
            table.AddColumn("Value");
            foreach (KeyValuePair<string, string> pair in map)
            {
                table.AddRow(Markup.Escape(pair.Key), Markup.Escape(pair.Value));
            }

            AnsiConsole.Write(table);
            return;
        }

        if (value is System.Collections.IEnumerable sequence and not string)
        {
            List<Dictionary<string, string>> rows = new List<Dictionary<string, string>>();
            foreach (object? item in sequence)
            {
                if (item is IReadOnlyDictionary<string, string> row)
                {
                    rows.Add(new Dictionary<string, string>(row));
                    continue;
                }

                if (item == null)
                {
                    continue;
                }

                Dictionary<string, string> properties = new Dictionary<string, string>();
                foreach (System.Reflection.PropertyInfo property in item.GetType().GetProperties())
                {
                    object? propertyValue = property.GetValue(item);
                    properties[property.Name] = Format(propertyValue);
                }

                rows.Add(properties);
            }

            if (rows.Count == 0)
            {
                AnsiConsole.MarkupLine("[grey]No rows.[/]");
                return;
            }

            Table grid = new Table();
            foreach (string column in rows[0].Keys)
            {
                grid.AddColumn(Markup.Escape(column));
            }

            foreach (Dictionary<string, string> row in rows)
            {
                grid.AddRow(row.Values.Select(Markup.Escape).ToArray());
            }

            AnsiConsole.Write(grid);
            return;
        }

        if (value != null)
        {
            Dictionary<string, string> properties = new Dictionary<string, string>();
            foreach (System.Reflection.PropertyInfo property in value.GetType().GetProperties())
            {
                properties[property.Name] = Format(property.GetValue(value));
            }

            if (properties.Count > 0)
            {
                Table table = new Table();
                table.AddColumn("Field");
                table.AddColumn("Value");
                foreach (KeyValuePair<string, string> pair in properties)
                {
                    table.AddRow(Markup.Escape(pair.Key), Markup.Escape(pair.Value));
                }

                AnsiConsole.Write(table);
                return;
            }
        }

        AnsiConsole.WriteLine(value?.ToString() ?? "");
    }

    private static string Format(object? value)
    {
        if (value == null)
        {
            return "";
        }

        if (value is System.Collections.IEnumerable sequence and not string)
        {
            List<string> parts = new List<string>();
            foreach (object? item in sequence)
            {
                parts.Add(item?.ToString() ?? "");
            }

            return string.Join(",", parts);
        }

        return value.ToString() ?? "";
    }
}
