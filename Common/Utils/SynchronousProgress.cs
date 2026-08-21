namespace MarmaladeLauncher.Common.Utils;

public class SynchronousProgress<T> : IProgress<T> {
    private readonly Action<T> _handler;

    public SynchronousProgress(Action<T> handler) {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public void Report(T value) {
        _handler(value);
    }
}