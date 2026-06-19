using Xabbo;
using Xabbo.GEarth;

namespace FullRoomSniper.Core;

public class Extension : GEarthExtension
{
    public RoomSniper RoomSniper { get; private set; }
    public event Action<string>? LogMessage;

    public Extension() : base(new GEarthOptions
    {
        Name = "Full Room Sniper",
        Description = "Attempts to enter full rooms",
        Author = "JoaninhaJNS",
        Version = "1.0.1"
    })
    {
        RoomSniper = new RoomSniper(this);
        RoomSniper.LogMessage += msg => LogMessage?.Invoke(msg);
    }

    protected override void OnConnected(ConnectedEventArgs e)
    {
        base.OnConnected(e);
        RoomSniper.Initialize();
    }

    protected override void OnDisconnected()
    {
        base.OnDisconnected();
        RoomSniper.Stop();
    }
}