using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace MiniCapture;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = "Local\\MiniCapture.SingleInstance.0A4EDC11";
    private const string PipeName = "MiniCapture.SingleInstance.0A4EDC11";
    private const uint AllowAnyForegroundProcess = 0xFFFFFFFF;
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _listenerTask;

    private SingleInstanceCoordinator(Mutex mutex, string pipeName)
    {
        _mutex = mutex;
        _pipeName = pipeName;
    }

    public static bool TryCreatePrimary(out SingleInstanceCoordinator? coordinator)
    {
        return TryCreate(MutexName, PipeName, out coordinator);
    }

    internal static bool TryCreate(string mutexName, string pipeName, out SingleInstanceCoordinator? coordinator)
    {
        var mutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            coordinator = null;
            return false;
        }

        coordinator = new SingleInstanceCoordinator(mutex, pipeName);
        return true;
    }

    public static bool NotifyPrimary(string[] arguments)
    {
        return NotifyPrimary(PipeName, arguments);
    }

    public static void GrantForegroundActivationToPrimary()
    {
        try
        {
            // The shell starts this secondary instance from an explicit open action.
            // Preserve that short-lived foreground permission while the image path is
            // handed to the already-running primary instance through the named pipe.
            _ = AllowSetForegroundWindow(AllowAnyForegroundProcess);
        }
        catch (EntryPointNotFoundException)
        {
        }
        catch (DllNotFoundException)
        {
        }
    }

    internal static bool NotifyPrimary(string pipeName, string[] arguments)
    {
        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(arguments));

        for (var attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
                client.Connect(250);
                client.Write(payload, 0, payload.Length);
                client.Flush();
                return true;
            }
            catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }

        return false;
    }

    public void StartListening(Action<string[]> onArgumentsReceived)
    {
        ArgumentNullException.ThrowIfNull(onArgumentsReceived);
        _listenerTask = ListenAsync(onArgumentsReceived);
    }

    public void Dispose()
    {
        _cancellation.Cancel();

        try
        {
            _listenerTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
        }

        _cancellation.Dispose();
        _mutex.Dispose();
    }

    private async Task ListenAsync(Action<string[]> onArgumentsReceived)
    {
        while (!_cancellation.IsCancellationRequested)
        {
            using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

            try
            {
                await server.WaitForConnectionAsync(_cancellation.Token);
                using var stream = new MemoryStream();
                await server.CopyToAsync(stream, _cancellation.Token);
                var arguments = JsonSerializer.Deserialize<string[]>(stream.ToArray()) ?? [];
                onArgumentsReceived(arguments);
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
            {
                return;
            }
            catch (IOException)
            {
            }
            catch (JsonException)
            {
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(uint dwProcessId);
}
