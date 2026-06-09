using Xabbo;
using Xabbo.GEarth;
using Xabbo.Messages.Flash;

var ext = new GEarthExtension(new GEarthOptions
{
    Name = "Full Room Sniper",
    Description = "Attempts to enter full rooms",
    Author = "JoaninhaJNS",
    Version = "1.0.0"
});

var formLock = new object();
Form? formUI = null;
int? currentRoomId = null;
bool startMonitor = false;
bool monitoring = false;
bool bruteForceActive = false;
CancellationTokenSource? cancellation = null;
Action<string>? uiLog = null;

void log(string message) => uiLog?.Invoke(message);

void StopMonitor(bool clearLogs = true)
{
    monitoring = false;
    startMonitor = false;
    bruteForceActive = false;
    currentRoomId = null;
    cancellation?.Cancel();
    cancellation = null;
    uiLog?.Invoke(clearLogs ? "\x00clear" : "\x00stop");
}

async Task SniffRoomCapacity(CancellationToken token)
{
    while (!token.IsCancellationRequested)
    {
        if (!bruteForceActive && currentRoomId.HasValue)
            ext.Send(Out.GetGuestRoom, currentRoomId.Value, 0, 0);

        await Task.Delay(200, token);
    }
}

async Task TryToEnterRoom(int roomId, CancellationToken token)
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

ext.Intercept(Out.GetGuestRoom, e =>
{
    if (!startMonitor || monitoring) return;

    int roomId = e.Packet.Read<int>();
    int isEntering = e.Packet.Read<int>();
    int forward = e.Packet.Read<int>();

    if (forward != 1) return;

    e.Block();

    currentRoomId = roomId;
    startMonitor = false;
    monitoring = true;

    log($"Monitoring room {roomId}...");

    cancellation = new CancellationTokenSource();
    Task.Run(() => SniffRoomCapacity(cancellation.Token));
});

ext.Intercept(In.GetGuestRoomResult, e =>
{
    if (!monitoring || currentRoomId == null) return;

    bool entered = e.Packet.Read<bool>();
    int roomId = e.Packet.Read<int>();
    string roomName = e.Packet.Read<string>();
    e.Packet.Read<int>();    // ownerId
    e.Packet.Read<string>(); // ownerName
    int doorMode = e.Packet.Read<int>();
    int userCount = e.Packet.Read<int>();
    int maxUserCount = e.Packet.Read<int>();

    if (roomId != currentRoomId.Value) return;

    if (entered)
    {
        log($"Entered room: {roomName}");
        StopMonitor(clearLogs: false);
        return;
    }

    if (bruteForceActive) return;

    if (doorMode != 0)
    {
        log("Room is locked, stopped!");
        StopMonitor(clearLogs: false);
        return;
    }

    if (userCount < maxUserCount)
    {
        bruteForceActive = true;
        log($"Slot available ({userCount}/{maxUserCount})! Entering...");
        Task.Run(() => TryToEnterRoom(currentRoomId.Value, cancellation!.Token));
    }
    else
    {
        log($"Room is full ({userCount}/{maxUserCount}), waiting...");
    }
});

Form CreateForm()
{
    var form = new Form
    {
        Text = "Full Room Sniper",
        StartPosition = FormStartPosition.CenterScreen,
        FormBorderStyle = FormBorderStyle.FixedDialog,
        MaximizeBox = false,
        Width = 380,
        Height = 260,
    };

    var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui", "icon.ico");
    form.Icon = File.Exists(iconPath) ? new Icon(iconPath) : Icon.ExtractAssociatedIcon(Application.ExecutablePath);

    var tooltip = new ToolTip();
    int padding = 12, y = 12;
    string lastMessage = "";

    bool pinned = false;
    var buttonPin = new Button
    {
        Text = "📌",
        FlatStyle = FlatStyle.Flat,
        Width = 24,
        Height = 24,
        Location = new Point(form.ClientSize.Width - 24 - padding, y),
        TabStop = false,
        Cursor = Cursors.Hand,
        Font = new Font("Segoe UI Symbol", 10f),
    };
    buttonPin.FlatAppearance.BorderSize = 0;
    buttonPin.FlatAppearance.MouseOverBackColor = SystemColors.ControlLight;
    tooltip.SetToolTip(buttonPin, "Always on top");
    buttonPin.Click += (_, _) =>
    {
        pinned = !pinned;
        form.TopMost = pinned;
        buttonPin.ForeColor = pinned ? SystemColors.HotTrack : SystemColors.ControlText;
        form.ActiveControl = null;
    };
    form.Controls.Add(buttonPin);

    var buttonStart = new Button
    {
        Text = "Start",
        FlatStyle = FlatStyle.Flat,
        Location = new Point(padding, y),
        Width = form.ClientSize.Width - padding * 2 - buttonPin.Width - 6,
        Height = 24,
        Cursor = Cursors.Hand,
    };
    buttonStart.FlatAppearance.BorderColor = Color.FromArgb(130, 130, 130);
    buttonStart.FlatAppearance.BorderSize = 1;
    form.Controls.Add(buttonStart);
    y += 32;

    var labelStatus = new Label
    {
        Text = "Status",
        AutoSize = true,
        Location = new Point(padding, y),
    };
    form.Controls.Add(labelStatus);
    y += labelStatus.PreferredHeight + 2;

    var panelLog = new Panel
    {
        Location = new Point(padding, y),
        Width = form.ClientSize.Width - padding * 2,
        Height = form.ClientSize.Height - y - padding,
        BorderStyle = BorderStyle.FixedSingle,
    };
    var richTextBoxLog = new RichTextBox
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        BorderStyle = BorderStyle.None,
        ScrollBars = RichTextBoxScrollBars.Vertical,
        ShortcutsEnabled = true,
        BackColor = SystemColors.Window,
        Font = new Font("Consolas", 8.5f),
    };
    panelLog.Controls.Add(richTextBoxLog);
    form.Controls.Add(panelLog);

    void setRunning(bool running) => buttonStart.Text = running ? "Stop" : "Start";

    void appendLine(string line)
    {
        richTextBoxLog.AppendText(line + Environment.NewLine);
        richTextBoxLog.ScrollToCaret();
    }

    void onStartStop()
    {
        if (monitoring || startMonitor)
        {
            StopMonitor();
            return;
        }

        richTextBoxLog.Clear();
        lastMessage = "";
        startMonitor = true;
        setRunning(true);
        log("Try to enter the room you want to target.");
    }

    buttonStart.Click += (_, _) =>
    {
        onStartStop();
        form.ActiveControl = null;
    };

    uiLog = message =>
    {
        if (richTextBoxLog.IsDisposed) return;

        void Handle()
        {
            if (message == "\x00clear")
            {
                richTextBoxLog.Clear();
                lastMessage = "";
                setRunning(false);
                return;
            }
            if (message == "\x00stop")
            {
                setRunning(false);
                return;
            }
            if (message == lastMessage) return;
            lastMessage = message;
            appendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        if (richTextBoxLog.InvokeRequired)
            richTextBoxLog.Invoke(Handle);
        else
            Handle();
    };

    form.Activated += (_, _) => form.ActiveControl = null;

    form.Shown += (_, _) =>
    {
        form.ActiveControl = null;
        form.BringToFront();
        form.Activate();
        form.TopMost = true;
        form.TopMost = false;
    };

    form.FormClosed += (_, _) =>
    {
        lock (formLock)
        {
            uiLog = null;
            formUI = null;
        }
    };

    return form;
}

void LoadForm()
{
    lock (formLock)
    {
        if (formUI != null && !formUI.IsDisposed)
        {
            formUI.Invoke(() =>
            {
                formUI.BringToFront();
                formUI.Activate();
            });
            return;
        }
    }

    var thread = new Thread(() =>
    {
        var form = CreateForm();
        lock (formLock)
        {
            formUI = form;
        }
        Application.Run(form);
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.IsBackground = true;
    thread.Start();
}

ext.Activated += () => LoadForm();

ext.Disconnected += () => StopMonitor();

ext.Run();