using System.IO.Pipes;
using System.Security.Principal;

namespace AutoPowerMode;

public sealed class SingleInstanceService : IDisposable
{
    private const string CommandOpenSettings = "OpenSettings";

    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly bool _ownsMutex;
    private readonly CancellationTokenSource _serverCancellation = new();
    private Task? _serverTask;

    private SingleInstanceService(Mutex mutex, bool ownsMutex, string pipeName)
    {
        _mutex = mutex;
        _ownsMutex = ownsMutex;
        _pipeName = pipeName;
    }

    public bool IsFirstInstance => _ownsMutex;

    public static SingleInstanceService Create()
    {
        var suffix = GetUserScopeSuffix();
        var mutexName = $@"Local\AutoPowerMode.SingleInstance.{suffix}";
        var pipeName = $"AutoPowerMode.SingleInstance.{suffix}";
        var mutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);

        return new SingleInstanceService(mutex, createdNew, pipeName);
    }

    public bool SignalExistingInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect(2_000);

            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(CommandOpenSettings);
            Logger.Info("已通知现有实例打开设置窗口。");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("通知现有实例失败。", ex);
            return false;
        }
    }

    public void StartActivationServer(Action activationRequested)
    {
        if (!IsFirstInstance || _serverTask is not null)
        {
            return;
        }

        _serverTask = Task.Run(() => ListenForActivationRequestsAsync(activationRequested, _serverCancellation.Token));
    }

    private async Task ListenForActivationRequestsAsync(Action activationRequested, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellationToken);

                using var reader = new StreamReader(server);
                var command = await reader.ReadLineAsync(cancellationToken);

                if (string.Equals(command, CommandOpenSettings, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Info("收到二次启动请求，打开设置窗口。");
                    activationRequested();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Error("单实例通信监听异常。", ex);
                await DelayAfterServerErrorAsync(cancellationToken);
            }
        }
    }

    private static async Task DelayAfterServerErrorAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static string GetUserScopeSuffix()
    {
        try
        {
            var sid = WindowsIdentity.GetCurrent().User?.Value;
            if (!string.IsNullOrWhiteSpace(sid))
            {
                return SanitizeNamePart(sid);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("读取当前用户 SID 失败，改用用户名生成单实例名称。", ex);
        }

        return SanitizeNamePart(Environment.UserName);
    }

    private static string SanitizeNamePart(string value)
    {
        var chars = value
            .Where(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_')
            .ToArray();

        return chars.Length > 0 ? new string(chars) : "CurrentUser";
    }

    public void Dispose()
    {
        _serverCancellation.Cancel();

        try
        {
            _serverTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            Logger.Error("停止单实例通信监听失败。", ex);
        }

        _serverCancellation.Dispose();

        if (_ownsMutex)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (Exception ex)
            {
                Logger.Error("释放单实例互斥锁失败。", ex);
            }
        }

        _mutex.Dispose();
    }
}
