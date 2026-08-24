using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using SyncPulse.Core.Enums;
using SyncPulse.Core.Packets;
using SyncPulse.Core.Protocol;

namespace SyncPulse.Client.Services
{
    public class ClientNetworkService : IDisposable
    {
        private TcpClient? _client;
        private NetworkStream? _stream;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private CancellationTokenSource? _cts;
        private Task? _readTask;
        private Task? _heartbeatTask;
        private uint _sequenceCounter = 1;

        private readonly ConcurrentDictionary<uint, TaskCompletionSource<SyncPacket>> _pendingResponses = new();

        public ClientSessionState Session { get; } = new();

        public bool IsConnected => _client != null && _client.Connected;

        public event Action? Connected;
        public event Action? Disconnected;
        public event Action<ChatMessagePacket>? MessageReceived;
        public event Action<MessageAckPacket>? MessageAckReceived;
        public event Action<CallSignalPacket>? CallSignalReceived;
        public event Action<string>? SystemBroadcastReceived;
        public event Action<UserPresenceChangedPacket>? PresenceChanged;
        public event Action<TypingIndicatorPacket>? TypingIndicatorReceived;

        public async Task<bool> ConnectAsync(string host, int port)
        {
            try
            {
                Disconnect();

                _cts = new CancellationTokenSource();
                _client = new TcpClient();
                await _client.ConnectAsync(host, port, _cts.Token);
                _stream = _client.GetStream();

                Session.ServerIP = host;
                Session.ServerPort = port;

                _readTask = Task.Run(() => ReadLoopAsync(_cts.Token));
                _heartbeatTask = Task.Run(() => HeartbeatLoopAsync(_cts.Token));

                Connected?.Invoke();
                return true;
            }
            catch
            {
                Disconnect();
                return false;
            }
        }

        public async Task SendPacketAsync(SyncPacket packet)
        {
            if (_stream == null || !IsConnected) return;

            await _sendLock.WaitAsync();
            try
            {
                await FrameStreamParser.WritePacketAsync(_stream, packet);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public async Task<TResponse?> SendRequestAsync<TRequest, TResponse>(PacketType type, TRequest payload, int timeoutMs = 5000)
            where TResponse : class
        {
            if (!IsConnected) return null;

            uint seq = Interlocked.Increment(ref _sequenceCounter);
            var packet = SyncPacket.Create(type, payload, seq);
            var tcs = new TaskCompletionSource<SyncPacket>(TaskCreationOptions.RunContinuationsAsynchronously);

            _pendingResponses[seq] = tcs;

            try
            {
                await SendPacketAsync(packet);

                using var timeoutCts = new CancellationTokenSource(timeoutMs);
                using (timeoutCts.Token.Register(() => tcs.TrySetCanceled()))
                {
                    var responsePacket = await tcs.Task;
                    return responsePacket.GetPayload<TResponse>();
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                _pendingResponses.TryRemove(seq, out _);
            }
        }

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _stream != null && IsConnected)
                {
                    var packet = await FrameStreamParser.ReadPacketAsync(_stream, ct);
                    if (packet == null) break;

                    // 1. فحص الردود المعلقة ذات رقم التسلسل المتطابق
                    if (_pendingResponses.TryRemove(packet.Header.SequenceNumber, out var tcs))
                    {
                        tcs.TrySetResult(packet);
                        continue;
                    }

                    // 2. توجيه الرسائل غير المتزامنة والأحداث اللحظية
                    switch (packet.Header.Type)
                    {
                        case PacketType.DirectChatMessage:
                            var chatMsg = packet.GetPayload<ChatMessagePacket>();
                            if (chatMsg != null) MessageReceived?.Invoke(chatMsg);
                            break;

                        case PacketType.MessageDeliveryAck:
                        case PacketType.MessageReadAck:
                            var ack = packet.GetPayload<MessageAckPacket>();
                            if (ack != null) MessageAckReceived?.Invoke(ack);
                            break;

                        case PacketType.CallOffer:
                        case PacketType.CallRinging:
                        case PacketType.CallAnswer:
                        case PacketType.CallReject:
                        case PacketType.CallBusy:
                        case PacketType.CallEnd:
                            var callSig = packet.GetPayload<CallSignalPacket>();
                            if (callSig != null)
                            {
                                if (packet.Header.Type == PacketType.CallAnswer) callSig.Action = CallAction.Accept;
                                else if (packet.Header.Type == PacketType.CallRinging) callSig.Action = CallAction.Ringing;
                                else if (packet.Header.Type == PacketType.CallReject) callSig.Action = CallAction.Reject;
                                else if (packet.Header.Type == PacketType.CallBusy) callSig.Action = CallAction.Busy;
                                else if (packet.Header.Type == PacketType.CallEnd) callSig.Action = CallAction.End;

                                CallSignalReceived?.Invoke(callSig);
                            }
                            break;

                        case PacketType.UserPresenceChanged:
                            var presence = packet.GetPayload<UserPresenceChangedPacket>();
                            if (presence != null) PresenceChanged?.Invoke(presence);
                            break;

                        case PacketType.TypingIndicator:
                            var typing = packet.GetPayload<TypingIndicatorPacket>();
                            if (typing != null) TypingIndicatorReceived?.Invoke(typing);
                            break;

                        case PacketType.ProtocolError:
                        case PacketType.AccessDenied:
                            string errorMsg = packet.GetPayload<string>() ?? "تنبيه من الخادم";
                            SystemBroadcastReceived?.Invoke(errorMsg);
                            break;

                        case PacketType.HeartbeatAck:
                            break;
                    }
                }
            }
            catch
            {
                // انقطاع الاتصال
            }
            finally
            {
                Disconnect();
            }
        }

        private async Task HeartbeatLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && IsConnected)
                {
                    await Task.Delay(15000, ct);
                    if (IsConnected)
                    {
                        var ping = new SyncPacket(PacketType.Heartbeat, Array.Empty<byte>());
                        await SendPacketAsync(ping);
                    }
                }
            }
            catch { }
        }

        public void Disconnect()
        {
            _cts?.Cancel();

            try { _stream?.Dispose(); } catch { }
            try { _client?.Close(); } catch { }

            _stream = null;
            _client = null;

            foreach (var kv in _pendingResponses)
            {
                kv.Value.TrySetCanceled();
            }
            _pendingResponses.Clear();

            Disconnected?.Invoke();
        }

        public void Dispose()
        {
            Disconnect();
            _sendLock.Dispose();
            _cts?.Dispose();
        }
    }
}
