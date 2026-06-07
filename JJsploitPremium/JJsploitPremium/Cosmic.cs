// ═══════════════════════════════════════════════════════════════════
//  Usage:
//    await Cosmic.Setup();        // downloads binaries, verifies hashes
//    Cosmic.Initialize();         // starts WebSocket server
//    Cosmic.Attach();             // injects into all Roblox processes
//    Cosmic.Execute("print('hello')");
//    Cosmic.SetAutoInject(true);  // auto-inject new Roblox instances
//    Cosmic.Shutdown();
// ═══════════════════════════════════════════════════════════════════

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;

namespace JJSploitPremium
{
public static class Cosmic
{
    public class Client
    {
        public int Pid { get; set; }
        public long UserId { get; set; }
        public long GameId { get; set; }
    }

    /// <summary>Fired when a module connects. Provides the process ID.</summary>
    public static event Action<int> OnClientConnected;

    /// <summary>Fired when a module disconnects. Provides the process ID.</summary>
    public static event Action<int> OnClientDisconnected;

    /// <summary>Fired when a module sends console output. (pid, kind, message) — kind: "print", "warn", "error"</summary>
    public static event Action<int, string, string> OnOutput;

    /// <summary>Fired when a module reports user info. (pid, userId, gameId)</summary>
    public static event Action<int, long, long> OnUserInfo;

    /// <summary>Fired when a module reports a setting value. (pid, name, value)</summary>
    public static event Action<int, string, string> OnModuleSetting;

    /// <summary>Fired when a module requests console clear.</summary>
    public static event Action OnClearConsole;

    /// <summary>Fired during Setup to report progress. (step, totalSteps, message)</summary>
    public static event Action<int, int, string> OnProgress;

    private const int WsPort = 24950;
    private const string DllUrl = "https://auth.cosmic.best/files/dll";
    private const string InjUrl = "https://auth.cosmic.best/files/injector";
    private const string SecureEngineUrl = "https://auth.cosmic.best/files/secureengine";
    private const string InfoUrl = "https://auth.cosmic.best/info";

    private static HttpListener _listener;
    private static CancellationTokenSource _cts;
    private static readonly ConcurrentDictionary<int, _ModuleClient> _clients = new ConcurrentDictionary<int, _ModuleClient>();
    private static bool _initialized;
    private static int _attaching;

    private static volatile bool _autoInjectEnabled;
    private static readonly HashSet<int> _injectedPids = new HashSet<int>();
    private static readonly object _injectedLock = new object();
    private static Thread _autoInjectThread;

    private static readonly string[] _robloxProcessNames = { "RobloxPlayerBeta", "Windows10Universal" };
    private static readonly HttpClient _http = new HttpClient();

    /// <summary>Whether the WebSocket server is running.</summary>
    public static bool IsRunning { get; private set; }

    /// <summary>Number of connected modules.</summary>
    public static int ClientCount => _clients.Count;

    /// <summary>Whether auto-inject is enabled.</summary>
    public static bool AutoInjectEnabled => _autoInjectEnabled;

    /// <summary>
    /// Resolve the bin directory (next to the running executable).
    /// </summary>
    public static string BinDirectory
    {
        get
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
                exePath = System.Reflection.Assembly.GetEntryAssembly()?.Location;
            string dir = Path.GetDirectoryName(exePath) ?? ".";
            return Path.Combine(dir, "bin");
        }
    }

    /// <summary>
    /// Download and verify all required binaries (Module.dll, Injector.exe, SecureEngineSDK64.dll).
    /// Uses SHA256 hashes from the info endpoint to skip up-to-date files.
    /// </summary>
    public static async Task Setup()
    {
        string bin = BinDirectory;
        Directory.CreateDirectory(bin);

        OnProgress?.Invoke(1, 4, "Checking for updates...");

        string dllHash = null, injHash = null;
        try
        {
            var resp = await _http.GetStringAsync(InfoUrl).ConfigureAwait(false);
            dllHash = _JsonString(resp, "dll_hash");
            injHash = _JsonString(resp, "inj_hash");
        }
        catch { }

        OnProgress?.Invoke(2, 4, "Refreshing binaries...");

        string modulePath = Path.Combine(bin, "Module.dll");
        bool needDll = !File.Exists(modulePath);
        if (!needDll && !string.IsNullOrEmpty(dllHash))
            needDll = Sha256File(modulePath) != dllHash;
        if (needDll)
        {
            var bytes = await _http.GetByteArrayAsync(DllUrl).ConfigureAwait(false);
            File.WriteAllBytes(modulePath, bytes);
        }

        string injectorPath = Path.Combine(bin, "Injector.exe");
        bool needInj = !File.Exists(injectorPath);
        if (!needInj && !string.IsNullOrEmpty(injHash))
            needInj = Sha256File(injectorPath) != injHash;
        if (needInj)
        {
            var bytes = await _http.GetByteArrayAsync(InjUrl).ConfigureAwait(false);
            File.WriteAllBytes(injectorPath, bytes);
        }

        OnProgress?.Invoke(3, 4, "Checking SecureEngine...");

        string secureEnginePath = Path.Combine(bin, "SecureEngineSDK64.dll");
        if (!File.Exists(secureEnginePath))
        {
            var bytes = await _http.GetByteArrayAsync(SecureEngineUrl).ConfigureAwait(false);
            File.WriteAllBytes(secureEnginePath, bytes);
        }

        OnProgress?.Invoke(4, 4, "Ready!");
    }

    /// <summary>
    /// Initialize Cosmic — starts the WebSocket server on port 24950.
    /// Call this once before using any other methods.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;

        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{WsPort}/");
        _listener.Start();
        IsRunning = true;
        _initialized = true;

        Task.Run(() => _AcceptLoop(_cts.Token));
        _StartAutoInjectThread();
    }

    /// <summary>
    /// Shutdown Cosmic — stops the WebSocket server and disconnects all modules.
    /// </summary>
    public static void Shutdown()
    {
        if (!_initialized) return;
        IsRunning = false;
        _initialized = false;
        _autoInjectEnabled = false;

        _cts?.Cancel();

        foreach (var client in _clients.Values)
        {
            try { client.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Shutdown", CancellationToken.None).Wait(1000); }
            catch { }
        }
        _clients.Clear();

        lock (_injectedLock) _injectedPids.Clear();

        try { _listener?.Stop(); } catch { }
        try { _listener?.Close(); } catch { }
    }

    /// <summary>
    /// Inject into all uninjected Roblox processes.
    /// Skips processes that are already connected.
    /// Returns the number of processes where injection was launched.
    /// </summary>
    public static int Attach()
    {
        _EnsureInit();

        if (Interlocked.CompareExchange(ref _attaching, 1, 0) != 0)
            throw new InvalidOperationException("Already attaching");

        try
        {
            string bin = BinDirectory;
            string injector = Path.Combine(bin, "Injector.exe");

            if (!File.Exists(injector))
                throw new FileNotFoundException("Injector not found");

            var allPids = GetRobloxProcesses();
            if (allPids.Count == 0)
                throw new InvalidOperationException("No Roblox instance found");

            var connected = new HashSet<int>(_clients.Keys);
            var toInject = allPids.Where(p => !connected.Contains(p)).ToList();

            if (toInject.Count == 0)
                throw new InvalidOperationException("All Roblox instances already injected");

            int injected = 0;
            foreach (int pid in toInject)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd",
                        Arguments = $"/c start \"\" /b Injector.exe {pid}",
                        WorkingDirectory = bin,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    })?.Dispose();
                    injected++;
                }
                catch { }
            }

            if (injected == 0)
                throw new InvalidOperationException("Failed to launch injector");

            return injected;
        }
        finally
        {
            Interlocked.Exchange(ref _attaching, 0);
        }
    }

    /// <summary>Get all running Roblox process IDs (whether injected or not).</summary>
    public static List<int> GetRobloxProcesses()
    {
        var pids = new List<int>();
        foreach (var name in _robloxProcessNames)
        {
            try
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    pids.Add(p.Id);
                    p.Dispose();
                }
            }
            catch { }
        }
        return pids;
    }

    /// <summary>Get all currently connected module clients with user/game info.</summary>
    public static List<Client> GetClients()
    {
        _EnsureInit();
        return _clients.Values.Select(c => new Client
        {
            Pid = c.Pid,
            UserId = c.UserId,
            GameId = c.GameId,
        }).ToList();
    }

    /// <summary>Get all currently connected (injected) module PIDs.</summary>
    public static List<int> GetClientPids()
    {
        _EnsureInit();
        return _clients.Keys.ToList();
    }

    /// <summary>Execute a Luau script on all connected modules.</summary>
    public static void Execute(string script)
    {
        _EnsureInit();
        if (_clients.IsEmpty)
            throw new InvalidOperationException("No clients connected");
        _BroadcastAsync(script).GetAwaiter().GetResult();
    }

    /// <summary>Execute a Luau script on all connected modules (async).</summary>
    public static async Task ExecuteAsync(string script)
    {
        _EnsureInit();
        if (_clients.IsEmpty)
            throw new InvalidOperationException("No clients connected");
        await _BroadcastAsync(script).ConfigureAwait(false);
    }

    /// <summary>Execute a Luau script on a specific module by PID.</summary>
    public static void Execute(int pid, string script)
    {
        _EnsureInit();
        if (!_clients.ContainsKey(pid))
            throw new InvalidOperationException("Client not connected");
        _SendToAsync(pid, script).GetAwaiter().GetResult();
    }

    /// <summary>Execute a Luau script on a specific module by PID (async).</summary>
    public static async Task ExecuteAsync(int pid, string script)
    {
        _EnsureInit();
        if (!_clients.ContainsKey(pid))
            throw new InvalidOperationException("Client not connected");
        await _SendToAsync(pid, script).ConfigureAwait(false);
    }

    /// <summary>Send a raw message to a specific client, or all clients if pid is null.</summary>
    public static void SendToClient(int? pid, string message)
    {
        _EnsureInit();
        if (pid.HasValue)
        {
            if (!_clients.ContainsKey(pid.Value))
                throw new InvalidOperationException("Client not connected");
            _SendToAsync(pid.Value, message).GetAwaiter().GetResult();
        }
        else
        {
            _BroadcastAsync(message).GetAwaiter().GetResult();
        }
    }

    /// <summary>Kill a process by PID.</summary>
    public static void Kill(int pid)
    {
        using (var proc = Process.Start(new ProcessStartInfo
        {
            FileName = "taskkill",
            Arguments = $"/F /PID {pid}",
            UseShellExecute = false,
            CreateNoWindow = true,
        }))
        {
            proc?.WaitForExit(5000);
        }
    }

    /// <summary>Kill all running Roblox processes. Returns the count killed.</summary>
    public static int KillAllRoblox()
    {
        var pids = GetRobloxProcesses();
        foreach (int pid in pids)
        {
            try { Kill(pid); } catch { }
        }
        return pids.Count;
    }

    /// <summary>Enable or disable auto-injection of new Roblox processes.</summary>
    public static void SetAutoInject(bool enabled)
    {
        _autoInjectEnabled = enabled;
    }

    /// <summary>Set the maximum FPS for all connected modules.</summary>
    public static void SetMaxFps(int fps) => _SendSetting("Max-Fps", fps.ToString());

    /// <summary>Enable or disable FPS unlock for all connected modules.</summary>
    public static void SetUnlockFps(bool enabled) => _SendSetting("Unlock-Fps", enabled ? "true" : "false");

    /// <summary>Enable or disable auto-execute for all connected modules.</summary>
    public static void SetAutoExecute(bool enabled) => _SendSetting("Auto-Execute", enabled ? "true" : "false");

    /// <summary>Enable or disable console redirect for all connected modules.</summary>
    public static void SetConsoleRedirect(bool enabled) => _SendSetting("Console-Redirect", enabled ? "true" : "false");

    /// <summary>
    /// Set the console filter for all connected modules.
    /// Values: "MESSAGE_NONE", "MESSAGE_OUTPUT", "MESSAGE_WARN", "MESSAGE_ERROR"
    /// </summary>
    public static void SetConsoleFilter(string filter) => _SendSetting("Console-Filter", filter);

    /// <summary>Translate an injector exit code to a human-readable message.</summary>
    public static string GetAttachStatusMessage(int exitCode)
    {
        switch (exitCode)
        {
            case 6: return "Success";
            case -1: return "Initialization failure";
            case 0: return "Failed to open Roblox process";
            case 1: return "Roblox version mismatch";
            case 2: return "Module DLL not found or corrupt";
            case 3: return "Memory operation failed";
            case 4: return "PDB download failed";
            case 5: return "Injection timeout";
            default: return $"Unknown exit code: {exitCode}";
        }
    }

    // ── Internals ────────────────────────────────────────────────────

    private class _ModuleClient
    {
        public int Pid;
        public WebSocket Socket;
        public long UserId;
        public long GameId;
        public readonly SemaphoreSlim SendLock = new SemaphoreSlim(1, 1);
    }

    private static string Sha256File(string path)
    {
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(path))
        {
            var hash = sha.ComputeHash(stream);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    private static void _StartAutoInjectThread()
    {
        if (_autoInjectThread != null && _autoInjectThread.IsAlive)
            return;

        _autoInjectThread = new Thread(() =>
        {
            var lastPids = new HashSet<int>(GetRobloxProcesses());

            while (_initialized)
            {
                Thread.Sleep(2000);

                var current = new HashSet<int>(GetRobloxProcesses());

                if (_autoInjectEnabled)
                {
                    var newPids = current.Where(p => !lastPids.Contains(p)).ToList();
                    if (newPids.Count > 0)
                    {
                        List<int> trulyNew;
                        lock (_injectedLock)
                        {
                            trulyNew = newPids.Where(p => !_injectedPids.Contains(p)).ToList();
                            foreach (int p in trulyNew)
                                _injectedPids.Add(p);
                        }

                        if (trulyNew.Count > 0)
                        {
                            string bin = BinDirectory;
                            string injector = Path.Combine(bin, "Injector.exe");
                            if (File.Exists(injector))
                            {
                                foreach (int pid in trulyNew)
                                {
                                    try
                                    {
                                        Process.Start(new ProcessStartInfo
                                        {
                                            FileName = "cmd",
                                            Arguments = $"/c start \"\" /b Injector.exe {pid}",
                                            WorkingDirectory = bin,
                                            UseShellExecute = false,
                                            CreateNoWindow = true,
                                        })?.Dispose();
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }

                lock (_injectedLock)
                    _injectedPids.RemoveWhere(p => !current.Contains(p));

                lastPids = current;
            }
        })
        {
            IsBackground = true,
            Name = "CosmicAutoInject",
        };
        _autoInjectThread.Start();
    }

    private static async Task _AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && IsRunning)
        {
            try
            {
                var context = await _listener.GetContextAsync().ConfigureAwait(false);

                if (!context.Request.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                    continue;
                }

                string pidHeader = context.Request.Headers["process-id"];
                if (!int.TryParse(pidHeader, out int pid))
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                    continue;
                }

                var wsContext = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
                var client = new _ModuleClient { Pid = pid, Socket = wsContext.WebSocket };
                _clients[pid] = client;

                OnClientConnected?.Invoke(pid);

                _ = Task.Run(() => _HandleClient(client, ct));
            }
            catch (ObjectDisposedException) { break; }
            catch (HttpListenerException) { break; }
            catch { }
        }
    }

    private static async Task _HandleClient(_ModuleClient client, CancellationToken ct)
    {
        var buffer = new byte[8192];

        try
        {
            while (client.Socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await client.Socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message;
                    if (result.EndOfMessage)
                    {
                        message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    }
                    else
                    {
                        using (var ms = new MemoryStream())
                        {
                            ms.Write(buffer, 0, result.Count);
                            while (!result.EndOfMessage)
                            {
                                result = await client.Socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
                                ms.Write(buffer, 0, result.Count);
                            }
                            message = Encoding.UTF8.GetString(ms.ToArray());
                        }
                    }

                    _ProcessMessage(client, message);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        catch { }
        finally
        {
            _clients.TryRemove(client.Pid, out _);
            OnClientDisconnected?.Invoke(client.Pid);
            try { client.Socket.Dispose(); } catch { }
        }
    }

    private static void _ProcessMessage(_ModuleClient client, string message)
    {
        if (string.IsNullOrEmpty(message) || message[0] != '{') return;

        try
        {
            if (message.Contains("\"UserId\"") && message.Contains("\"GameId\""))
            {
                client.UserId = _JsonLong(message, "UserId");
                client.GameId = _JsonLong(message, "GameId");
                OnUserInfo?.Invoke(client.Pid, client.UserId, client.GameId);
                return;
            }

            string type = _JsonString(message, "Type");
            switch (type)
            {
                case "Setting":
                    string settingName = _JsonString(message, "Name");
                    string settingValue = _JsonString(message, "Value");
                    if (settingName != null)
                        OnModuleSetting?.Invoke(client.Pid, settingName, settingValue);
                    break;

                case "Output":
                    int status = _JsonInt(message, "Status");
                    string text = _JsonString(message, "Message");
                    string kind;
                    switch (status)
                    {
                        case 2: kind = "error"; break;
                        case 1: kind = "warn"; break;
                        default: kind = "print"; break;
                    }
                    if (text != null)
                        OnOutput?.Invoke(client.Pid, kind, text);
                    break;

                case "ClearConsole":
                    OnClearConsole?.Invoke();
                    break;

                default:
                    if (message.Contains("\"Status\"") && message.Contains("\"Message\""))
                    {
                        int s = _JsonInt(message, "Status");
                        string msg = _JsonString(message, "Message");
                        string k;
                        switch (s)
                        {
                            case 2: k = "error"; break;
                            case 1: k = "warn"; break;
                            default: k = "print"; break;
                        }
                        if (msg != null)
                            OnOutput?.Invoke(client.Pid, k, msg);
                    }
                    break;
            }
        }
        catch { }
    }

    private static async Task _BroadcastAsync(string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        var segment = new ArraySegment<byte>(bytes);

        var tasks = new List<Task>();
        foreach (var client in _clients.Values.ToArray())
        {
            if (client.Socket.State == WebSocketState.Open)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await client.SendLock.WaitAsync().ConfigureAwait(false);
                    try { await client.Socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false); }
                    catch { }
                    finally { client.SendLock.Release(); }
                }));
            }
        }

        if (tasks.Count > 0)
            await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async Task _SendToAsync(int pid, string message)
    {
        if (_clients.TryGetValue(pid, out var client) && client.Socket.State == WebSocketState.Open)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await client.SendLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await client.Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
            }
            finally { client.SendLock.Release(); }
        }
    }

    private static void _SendSetting(string name, object value)
    {
        _EnsureInit();

        string json;
        if (value is bool b)
            json = $"{{\"Name\":\"{name}\",\"Value\":{(b ? "true" : "false")}}}";
        else
            json = $"{{\"Name\":\"{name}\",\"Value\":\"{value}\"}}";

        _BroadcastAsync(json).GetAwaiter().GetResult();
    }

    private static void _EnsureInit()
    {
        if (!_initialized)
            throw new InvalidOperationException("Cosmic.Initialize() must be called first.");
    }

    private static string _JsonString(string json, string key)
    {
        string search = $"\"{key}\"";
        int idx = json.IndexOf(search, StringComparison.Ordinal);
        if (idx < 0) return null;
        idx = json.IndexOf(':', idx + search.Length);
        if (idx < 0) return null;
        idx = json.IndexOf('"', idx + 1);
        if (idx < 0) return null;
        int end = json.IndexOf('"', idx + 1);
        if (end < 0) return null;
        return json.Substring(idx + 1, end - idx - 1)
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t")
            .Replace("\\\"", "\"")
            .Replace("\\\\", "\\");
    }

    private static int _JsonInt(string json, string key)
    {
        string search = $"\"{key}\"";
        int idx = json.IndexOf(search, StringComparison.Ordinal);
        if (idx < 0) return 0;
        idx = json.IndexOf(':', idx + search.Length);
        if (idx < 0) return 0;
        idx++;
        while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t')) idx++;
        int start = idx;
        while (idx < json.Length && (char.IsDigit(json[idx]) || json[idx] == '-')) idx++;
        if (int.TryParse(json.Substring(start, idx - start), out int val))
            return val;
        return 0;
    }

    private static long _JsonLong(string json, string key)
    {
        string search = $"\"{key}\"";
        int idx = json.IndexOf(search, StringComparison.Ordinal);
        if (idx < 0) return 0;
        idx = json.IndexOf(':', idx + search.Length);
        if (idx < 0) return 0;
        idx++;
        while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t')) idx++;
        int start = idx;
        while (idx < json.Length && (char.IsDigit(json[idx]) || json[idx] == '-')) idx++;
        if (long.TryParse(json.Substring(start, idx - start), out long val))
            return val;
        return 0;
    }
}
}