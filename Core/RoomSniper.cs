using Xabbo;
using Xabbo.Messages.Flash;

namespace FullRoomSniper.Core;

public class RoomSniper(Extension ext)
{
    private readonly Extension ext = ext;
    private CancellationTokenSource? cancellation;
    public bool IsMonitoring { get; private set; } = false;
    public bool StartMonitor { get; set; } = false;
    public bool BruteForceActive { get; private set; } = false;
    public int? CurrentRoomId { get; private set; } = null;
    public event Action<string>? LogMessage = null;
    private void Log(string message) => LogMessage?.Invoke(message);

    public void Initialize()
    {
        ext.Intercept(Out.GetGuestRoom, HandleEnterRoom);
        ext.Intercept(In.GetGuestRoomResult, HandleRoomResult);
    }

    private void Start(int roomId)
    {
        CurrentRoomId = roomId;
        IsMonitoring = true;
        BruteForceActive = false;

        Log($"Monitoring room {roomId}...");

        cancellation = new CancellationTokenSource();
        Task.Run(() => SniffRoomCapacity(cancellation.Token));
    }

    public void Stop(bool clearLogs = true)
    {
        StartMonitor = false;
        IsMonitoring = false;
        BruteForceActive = false;
        CurrentRoomId = null;
        cancellation?.Cancel();
        cancellation?.Dispose();
        cancellation = null;

        LogMessage?.Invoke(clearLogs ? "\x00clear" : "\x00stop");
    }

    public void HandleEnterRoom(Intercept e)
    {
        int roomId = e.Packet.Read<int>();
        e.Packet.Read<int>();
        int forward = e.Packet.Read<int>();

        if (forward != 1) return;

        e.Block();
        Start(roomId);
    }

    public void HandleRoomResult(Intercept e)
    {
        if (!IsMonitoring || !CurrentRoomId.HasValue) return;

        e.Block();
        bool entered = e.Packet.Read<bool>();
        int roomId = e.Packet.Read<int>();
        string roomName = e.Packet.Read<string>();
        e.Packet.Read<int>();
        e.Packet.Read<string>();
        int doorMode = e.Packet.Read<int>();
        int userCount = e.Packet.Read<int>();
        int maxUserCount = e.Packet.Read<int>();

        if (roomId != CurrentRoomId.Value) return;

        if (entered)
        {
            Log($"Entered room: {roomName}");
            Stop(clearLogs: false);
            return;
        }

        if (BruteForceActive) return;

        if (doorMode != 0)
        {
            Log("Room is locked, stopped!");
            Stop(clearLogs: false);
            return;
        }

        if (userCount < maxUserCount)
        {
            BruteForceActive = true;
            Log($"Slot available ({userCount}/{maxUserCount})! Entering...");
            Task.Run(() => TryToEnterRoom(CurrentRoomId.Value, cancellation!.Token));
        }
        else
        {
            Log($"Room is full ({userCount}/{maxUserCount}), waiting...");
        }
    }

    private async Task SniffRoomCapacity(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (!BruteForceActive && CurrentRoomId.HasValue)
                ext.Send(Out.GetGuestRoom, CurrentRoomId.Value, 0, 0);
            await Task.Delay(200, token);
        }
    }

    private async Task TryToEnterRoom(int roomId, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            ext.Send(Out.GetGuestRoom, roomId, 0, 1);
            try
            {
                await ext.ReceiveAsync(In.RoomReady, timeout: 1000, cancellationToken: token);
                break;
            }
            catch (TimeoutException) { }
        }
    }
}
