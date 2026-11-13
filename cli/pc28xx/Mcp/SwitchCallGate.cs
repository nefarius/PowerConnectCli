using System.Collections.Concurrent;

namespace Nefarius.PowerConnect.Cli.Mcp;

/// <summary>
///     Serializes calls that share a switch alias and lets different aliases run together.
/// </summary>
internal sealed class SwitchCallGate
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

    public async Task<T> RunAsync<T>(string alias, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        SemaphoreSlim gate = _gates.GetOrAdd(alias, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await action(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }
}
