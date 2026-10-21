namespace Woodie.Rendering;

// Workers only receive immutable pixel jobs. A generation guard also invalidates already queued UI callbacks.
internal sealed class RefinementGate
{
    CancellationTokenSource source;
    readonly object sync = new();
    long generation;
    public Task Work { get; private set; } = Task.CompletedTask;

    public void Cancel()
    {
        lock (sync)
        {
            Interlocked.Increment(ref generation);
            source?.Cancel();
            source = null;
        }
    }

    public void Start(Action<CancellationToken> work, Action<Action> dispatch, Action complete, Action<Exception> failed)
    {
        Cancel();
        long current = Interlocked.Read(ref generation);
        var cts = new CancellationTokenSource();
        lock (sync) source = cts;
        Work = Task.Run(() =>
        {
            try
            {
                work(cts.Token);
                if (!cts.IsCancellationRequested) dispatch(() => { if (Interlocked.Read(ref generation) == current) complete(); });
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
            catch (Exception ex) { dispatch(() => { if (Interlocked.Read(ref generation) == current) failed(ex); }); }
            finally
            {
                lock (sync)
                {
                    if (source == cts) source = null;
                    cts.Dispose();
                }
            }
        });
    }
}
