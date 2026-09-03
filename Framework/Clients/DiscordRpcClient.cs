using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using StardewDiscordRPC.Framework.Models;

namespace StardewDiscordRPC.Framework.Clients
{
    public class DiscordRpcClient : IDisposable
    {
        private readonly string applicationId;
        private readonly Action<string, bool> logger;
        private readonly CancellationTokenSource cts = new();
        private readonly object syncLock = new();
        private readonly SemaphoreSlim writeLock = new(1, 1);

        private NamedPipeClientStream? pipeStream;
        private DiscordActivity? pendingActivity;
        private bool isConnected;
        private int processId;

        public event Action<string>? OnJoin;
        public event Action<DiscordUser>? OnJoinRequest;

        public bool IsConnected => isConnected;

        public DiscordRpcClient(string applicationId, Action<string, bool> logger)
        {
            this.applicationId = applicationId;
            this.logger = logger;

            try
            {
                this.processId = Process.GetCurrentProcess().Id;
            }
            catch
            {
                this.processId = 0;
            }

            Task.Run(WorkerLoop);
        }

        public void SetActivity(DiscordActivity? activity)
        {
            lock (syncLock)
            {
                pendingActivity = activity;
            }
        }

        public void ClearActivity()
        {
            SetActivity(null);
        }

        public async Task RespondJoinRequestAsync(string userId, bool accept)
        {
            string cmd = accept ? "SEND_ACTIVITY_JOIN_INVITE" : "CLOSE_ACTIVITY_JOIN_REQUEST";
            var payload = new
            {
                cmd = cmd,
                args = new
                {
                    user_id = userId
                },
                nonce = Guid.NewGuid().ToString()
            };

            string json = JsonSerializer.Serialize(payload);
            await SendFrameAsync(DiscordOpCode.Frame, json);
        }

        private async Task WorkerLoop()
        {
            DiscordActivity? lastSentActivity = null;

            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    if (!isConnected || pipeStream == null || !pipeStream.IsConnected)
                    {
                        bool connected = await TryConnectAsync();
                        if (!connected)
                        {
                            await Task.Delay(5000, cts.Token);
                            continue;
                        }
                    }

                    if (isConnected && pipeStream != null && pipeStream.IsConnected)
                    {
                        DiscordActivity? activityToSend = null;
                        lock (syncLock)
                        {
                            if (!AreActivitiesEqual(pendingActivity, lastSentActivity))
                            {
                                activityToSend = pendingActivity;
                            }
                        }

                        if (activityToSend != null || (pendingActivity == null && lastSentActivity != null))
                        {
                            await SendActivityPayloadAsync(activityToSend);
                            lastSentActivity = activityToSend;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger($"[DiscordRPC] Pipe exception: {ex.Message}", false);
                    ClosePipe();
                }

                try
                {
                    await Task.Delay(2000, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            ClosePipe();
        }

        private async Task<bool> TryConnectAsync()
        {
            ClosePipe();

            for (int i = 0; i < 10; i++)
            {
                if (cts.Token.IsCancellationRequested)
                {
                    return false;
                }

                string pipeName = $"discord-ipc-{i}";
                try
                {
                    var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                    await client.ConnectAsync(60, cts.Token);

                    pipeStream = client;

                    var handshakeObj = new
                    {
                        v = 1,
                        client_id = applicationId
                    };

                    string handshakeJson = JsonSerializer.Serialize(handshakeObj);
                    await SendFrameAsync(DiscordOpCode.Handshake, handshakeJson);

                    var (op, response) = await ReadFrameAsync();
                    if (op == DiscordOpCode.Frame)
                    {
                        isConnected = true;
                        logger($"[DiscordRPC] Connected to Discord IPC on {pipeName}", true);

                        _ = Task.Run(ReadLoop);
                        return true;
                    }
                }
                catch
                {
                    // Try next pipe index
                }
            }

            return false;
        }

        private async Task SendSubscribeAsync(string evtName)
        {
            try
            {
                var payload = new
                {
                    cmd = "SUBSCRIBE",
                    evt = evtName,
                    nonce = Guid.NewGuid().ToString()
                };

                string json = JsonSerializer.Serialize(payload);
                await SendFrameAsync(DiscordOpCode.Frame, json);
            }
            catch
            {
            }
        }

        private async Task ReadLoop()
        {
            while (isConnected && pipeStream != null && pipeStream.IsConnected && !cts.Token.IsCancellationRequested)
            {
                try
                {
                    var (op, response) = await ReadFrameAsync();
                    if (op == DiscordOpCode.Close || string.IsNullOrEmpty(response))
                    {
                        break;
                    }

                    if (op == DiscordOpCode.Frame)
                    {
                        HandleIncomingFrame(response);
                    }
                }
                catch
                {
                    break;
                }
            }
        }

        private void HandleIncomingFrame(string json)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("evt", out var evtProp))
                {
                    string? evt = evtProp.GetString();
                    if (evt == "ERROR")
                    {
                        logger($"[DiscordRPC] Error frame: {json}", false);
                    }
                    else if (evt == "ACTIVITY_JOIN" && root.TryGetProperty("data", out var dataProp))
                    {
                        if (dataProp.TryGetProperty("secret", out var secretProp))
                        {
                            string? secret = secretProp.GetString();
                            if (!string.IsNullOrEmpty(secret))
                            {
                                logger($"[DiscordRPC] Received ACTIVITY_JOIN with secret: {secret}", true);
                                OnJoin?.Invoke(secret);
                            }
                        }
                    }
                    else if (evt == "ACTIVITY_JOIN_REQUEST" && root.TryGetProperty("data", out var reqData))
                    {
                        if (reqData.TryGetProperty("user", out var userProp))
                        {
                            var user = JsonSerializer.Deserialize<DiscordUser>(userProp.GetRawText());
                            if (user != null)
                            {
                                logger($"[DiscordRPC] Received ACTIVITY_JOIN_REQUEST from user: {user.Username}", true);
                                OnJoinRequest?.Invoke(user);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger($"[DiscordRPC] Error parsing frame: {ex.Message}", false);
            }
        }

        private async Task SendActivityPayloadAsync(DiscordActivity? activity)
        {
            var payload = new
            {
                cmd = "SET_ACTIVITY",
                args = new
                {
                    pid = processId,
                    activity = activity
                },
                nonce = Guid.NewGuid().ToString()
            };

            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            string json = JsonSerializer.Serialize(payload, options);
            logger($"[DiscordRPC] Sent SET_ACTIVITY to Discord: {activity?.Details ?? "null"} | {activity?.State ?? "null"}", true);
            await SendFrameAsync(DiscordOpCode.Frame, json);
        }

        private async Task SendFrameAsync(DiscordOpCode opCode, string json)
        {
            if (pipeStream == null || !pipeStream.IsConnected)
            {
                return;
            }

            await writeLock.WaitAsync(cts.Token);
            try
            {
                if (pipeStream == null || !pipeStream.IsConnected)
                {
                    return;
                }

                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
                byte[] header = new byte[8];

                int op = (int)opCode;
                int length = jsonBytes.Length;

                header[0] = (byte)(op & 0xFF);
                header[1] = (byte)((op >> 8) & 0xFF);
                header[2] = (byte)((op >> 16) & 0xFF);
                header[3] = (byte)((op >> 24) & 0xFF);

                header[4] = (byte)(length & 0xFF);
                header[5] = (byte)((length >> 8) & 0xFF);
                header[6] = (byte)((length >> 16) & 0xFF);
                header[7] = (byte)((length >> 24) & 0xFF);

                await pipeStream.WriteAsync(header, 0, 8, cts.Token);
                await pipeStream.WriteAsync(jsonBytes, 0, jsonBytes.Length, cts.Token);
                await pipeStream.FlushAsync(cts.Token);
            }
            finally
            {
                writeLock.Release();
            }
        }

        private async Task<(DiscordOpCode, string)> ReadFrameAsync()
        {
            if (pipeStream == null || !pipeStream.IsConnected)
            {
                return (DiscordOpCode.Close, string.Empty);
            }

            byte[] header = new byte[8];
            int read = 0;
            while (read < 8)
            {
                int r = await pipeStream.ReadAsync(header, read, 8 - read, cts.Token);
                if (r <= 0)
                {
                    return (DiscordOpCode.Close, string.Empty);
                }
                read += r;
            }

            int op = header[0] | (header[1] << 8) | (header[2] << 16) | (header[3] << 24);
            int length = header[4] | (header[5] << 8) | (header[6] << 16) | (header[7] << 24);

            if (length < 0 || length > 65536)
            {
                return (DiscordOpCode.Close, string.Empty);
            }

            byte[] buffer = new byte[length];
            read = 0;
            while (read < length)
            {
                int r = await pipeStream.ReadAsync(buffer, read, length - read, cts.Token);
                if (r <= 0)
                {
                    return (DiscordOpCode.Close, string.Empty);
                }
                read += r;
            }

            string content = Encoding.UTF8.GetString(buffer);
            return ((DiscordOpCode)op, content);
        }

        private bool AreActivitiesEqual(DiscordActivity? a, DiscordActivity? b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;

            return a.Details == b.Details &&
                   a.State == b.State &&
                   a.Assets?.LargeImage == b.Assets?.LargeImage &&
                   a.Assets?.LargeText == b.Assets?.LargeText &&
                   a.Assets?.SmallImage == b.Assets?.SmallImage &&
                   a.Assets?.SmallText == b.Assets?.SmallText &&
                   a.Timestamps?.Start == b.Timestamps?.Start &&
                   a.Party?.Size?[0] == b.Party?.Size?[0] &&
                   a.Party?.Size?[1] == b.Party?.Size?[1];
        }

        private void ClosePipe()
        {
            isConnected = false;
            try
            {
                pipeStream?.Dispose();
            }
            catch
            {
            }
            pipeStream = null;
        }

        public void Dispose()
        {
            cts.Cancel();
            ClosePipe();
            cts.Dispose();
        }
    }
}
