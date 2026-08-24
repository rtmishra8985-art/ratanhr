using System.Net.Sockets;
using System.Text;
using HRMS.Application.Interfaces.Biometric;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Biometric;

/// <summary>
/// ZKTeco biometric device provider — implements the ZKLib binary protocol over TCP.
/// Default port: 4370.  Protocol: ZKTeco SDK V2 (UDP/TCP command set).
///
/// Circuit Breaker: after <see cref="MaxConsecutiveFailures"/> connection failures the
/// provider stops attempting connects for <see cref="CircuitOpenDurationSeconds"/> seconds,
/// then resets to half-open on the next call.
/// </summary>
public sealed class ZKTecoProvider : IBiometricProvider
{
    // ── ZKTeco binary protocol constants ─────────────────────────────────────
    private const ushort CMD_CONNECT        = 1000;
    private const ushort CMD_EXIT           = 1001;
    private const ushort CMD_ACK_OK         = 2000;
    private const ushort CMD_ACK_ERROR      = 2001;
    private const ushort CMD_ACK_UNAUTH     = 2002;
    private const ushort CMD_ATTLOG_RRQ     = 13;
    private const ushort CMD_DATA           = 15;
    private const ushort CMD_PREPARE_DATA   = 16;
    private const ushort CMD_DATA_WRRQ      = 23;
    private const ushort CMD_FREE_DATA      = 32;

    // Each attendance log record is 40 bytes in the ZKTeco v2 format.
    private const int ATT_RECORD_SIZE = 40;

    // ── Circuit breaker state ─────────────────────────────────────────────────
    private const int MaxConsecutiveFailures   = 3;
    private const int CircuitOpenDurationSeconds = 60;

    private int      _consecutiveFailures;
    private DateTime _circuitOpenUntil = DateTime.MinValue;
    private readonly object _cbLock = new();

    // ── Configuration ─────────────────────────────────────────────────────────
    private readonly string _host;
    private readonly int    _port;
    private readonly int    _connectTimeoutMs;
    private readonly ILogger<ZKTecoProvider> _logger;

    public string VendorName => "ZKTeco";

    public ZKTecoProvider(ILogger<ZKTecoProvider> logger)
    {
        _logger           = logger;
        _host             = Environment.GetEnvironmentVariable("ZKTECO_DEVICE_IP")   ?? "";
        _port             = int.TryParse(Environment.GetEnvironmentVariable("ZKTECO_DEVICE_PORT"), out var p) ? p : 4370;
        _connectTimeoutMs = int.TryParse(Environment.GetEnvironmentVariable("ZKTECO_CONNECT_TIMEOUT_MS"), out var t) ? t : 5000;
    }

    // ── IBiometricProvider ────────────────────────────────────────────────────

    public async Task<IReadOnlyList<BiometricPunchLog>> FetchLogsAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        if (IsCircuitOpen())
        {
            _logger.LogWarning("[ZKTeco] Circuit breaker OPEN — skipping connect to {Host}:{Port}", _host, _port);
            return Array.Empty<BiometricPunchLog>();
        }

        try
        {
            var logs = await FetchLogsInternalAsync(from, to, ct).ConfigureAwait(false);
            ResetCircuit();
            return logs;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            RecordFailure();
            _logger.LogError(ex, "[ZKTeco] FetchLogsAsync failed for {Host}:{Port} — returning empty log set", _host, _port);
            return Array.Empty<BiometricPunchLog>();
        }
    }

    // Intentionally not `async`: this method performs no awaited work. Declaring it
    // `async` produced CS1998. The Task-returning shape is preserved, and the
    // unsupported-operation failure is surfaced through a faulted Task so callers
    // observe it on `await` exactly as before.
    public Task<int> SyncUsersAsync(
        IReadOnlyList<BiometricUser> users, CancellationToken ct = default)
    {
        if (IsCircuitOpen())
        {
            _logger.LogWarning("[ZKTeco] Circuit breaker OPEN — skipping user sync to {Host}:{Port}", _host, _port);
            return Task.FromResult(0);
        }

        // Do not report an unsupported operation as a successful zero-count sync.
        // The ZKTeco attendance-log protocol is implemented above, but roster
        // synchronization requires the vendor's enrollment/template protocol and
        // has not been approved for this release.
        _logger.LogWarning(
            "[ZKTeco] SyncUsersAsync is not supported in this release. " +
            "Roster synchronization was requested for {Count} users.", users.Count);
        return Task.FromException<int>(new NotSupportedException(
            "ZKTeco user synchronization is not available in this release. " +
            "Attendance log import and device status are supported."));
    }

    public async Task<BiometricDeviceStatus> GetDeviceStatusAsync(CancellationToken ct = default)
    {
        if (IsCircuitOpen())
            return new BiometricDeviceStatus(false, null, null,
                $"ZKTeco circuit breaker OPEN until {_circuitOpenUntil:HH:mm:ss} UTC.");

        try
        {
            using var client = await ConnectAsync(ct).ConfigureAwait(false);
            var session = await HandshakeAsync(client, ct).ConfigureAwait(false);
            if (session == null)
                return new BiometricDeviceStatus(false, null, null, "ZKTeco handshake rejected.");

            await SendExitAsync(client, session.Value, ct).ConfigureAwait(false);
            ResetCircuit();
            return new BiometricDeviceStatus(true, null, null, null);
        }
        catch (Exception ex)
        {
            RecordFailure();
            return new BiometricDeviceStatus(false, null, null, ex.Message);
        }
    }

    // ── Internal protocol implementation ─────────────────────────────────────

    private async Task<IReadOnlyList<BiometricPunchLog>> FetchLogsInternalAsync(
        DateTime from, DateTime to, CancellationToken ct)
    {
        using var client = await ConnectAsync(ct).ConfigureAwait(false);
        var stream = client.GetStream();

        var session = await HandshakeAsync(client, ct).ConfigureAwait(false);
        if (session == null)
        {
            _logger.LogWarning("[ZKTeco] Handshake failed — device at {Host}:{Port} refused connection.", _host, _port);
            return Array.Empty<BiometricPunchLog>();
        }

        ushort sessionId = session.Value;
        ushort replyId   = 1;

        // Request attendance log data
        var requestData = BuildAttLogRequest(from, to);
        var requestPacket = BuildPacket(CMD_ATTLOG_RRQ, sessionId, replyId++, requestData);
        await stream.WriteAsync(requestPacket, ct).ConfigureAwait(false);

        var responseHeader = await ReadPacketAsync(stream, 8, ct).ConfigureAwait(false);
        var cmd = BitConverter.ToUInt16(responseHeader, 0);

        if (cmd == CMD_PREPARE_DATA)
        {
            var dataSize = BitConverter.ToInt32(responseHeader, 8 < responseHeader.Length ? 8 : 4);
            var rawLogs  = await ReadAllDataAsync(stream, sessionId, replyId, dataSize, ct).ConfigureAwait(false);
            await SendExitAsync(client, sessionId, ct).ConfigureAwait(false);
            return ParseAttLogs(rawLogs);
        }

        if (cmd == CMD_DATA)
        {
            var payloadLen = BitConverter.ToUInt16(responseHeader, 4);
            var rawLogs    = await ReadPacketAsync(stream, payloadLen, ct).ConfigureAwait(false);
            await SendExitAsync(client, sessionId, ct).ConfigureAwait(false);
            return ParseAttLogs(rawLogs);
        }

        _logger.LogWarning("[ZKTeco] Unexpected response command {Cmd} for att-log request.", cmd);
        await SendExitAsync(client, sessionId, ct).ConfigureAwait(false);
        return Array.Empty<BiometricPunchLog>();
    }

    private async Task<TcpClient> ConnectAsync(CancellationToken ct)
    {
        var client = new TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_connectTimeoutMs);
        await client.ConnectAsync(_host, _port, timeoutCts.Token).ConfigureAwait(false);
        return client;
    }

    private static async Task<ushort?> HandshakeAsync(TcpClient client, CancellationToken ct)
    {
        var stream = client.GetStream();

        // CMD_CONNECT with session=0, reply=0, no data
        var connectPacket = BuildPacket(CMD_CONNECT, 0, 0, Array.Empty<byte>());
        await stream.WriteAsync(connectPacket, ct).ConfigureAwait(false);

        var response = await ReadPacketAsync(stream, 16, ct).ConfigureAwait(false);
        var ackCmd   = BitConverter.ToUInt16(response, 0);

        if (ackCmd != CMD_ACK_OK && ackCmd != CMD_ACK_UNAUTH)
            return null;

        // Session ID is returned in bytes 4-5 of the reply
        var sessionId = BitConverter.ToUInt16(response, 4);
        return sessionId;
    }

    private static async Task SendExitAsync(TcpClient client, ushort sessionId, CancellationToken ct)
    {
        try
        {
            var exitPacket = BuildPacket(CMD_EXIT, sessionId, 0, Array.Empty<byte>());
            await client.GetStream().WriteAsync(exitPacket, ct).ConfigureAwait(false);
        }
        catch { /* best-effort disconnect */ }
    }

    private static async Task<byte[]> ReadAllDataAsync(
        NetworkStream stream, ushort sessionId, ushort replyId, int expectedSize, CancellationToken ct)
    {
        var buffer = new List<byte>(expectedSize);

        while (buffer.Count < expectedSize)
        {
            // Free-data request to get the next chunk
            var req = BuildPacket(CMD_DATA_WRRQ, sessionId, replyId++, Array.Empty<byte>());
            await stream.WriteAsync(req, ct).ConfigureAwait(false);

            var header = await ReadPacketAsync(stream, 8, ct).ConfigureAwait(false);
            var cmd    = BitConverter.ToUInt16(header, 0);
            if (cmd != CMD_DATA) break;

            var chunkLen = BitConverter.ToUInt16(header, 6);
            var chunk    = await ReadPacketAsync(stream, chunkLen, ct).ConfigureAwait(false);
            buffer.AddRange(chunk);
        }

        // Free data buffer on device
        var freePacket = BuildPacket(CMD_FREE_DATA, sessionId, replyId++, Array.Empty<byte>());
        await stream.WriteAsync(freePacket, ct).ConfigureAwait(false);

        return buffer.ToArray();
    }

    // ── Packet helpers ────────────────────────────────────────────────────────

    private static byte[] BuildPacket(ushort command, ushort sessionId, ushort replyId, byte[] data)
    {
        // Header: [cmd:2le][checksum:2le][session:2le][reply:2le] + data
        var header    = new byte[8];
        var packet    = new byte[8 + data.Length];
        BitConverter.GetBytes(command).CopyTo(header, 0);
        BitConverter.GetBytes(sessionId).CopyTo(header, 4);
        BitConverter.GetBytes(replyId).CopyTo(header, 6);
        var checksum  = CalcChecksum(header, data);
        BitConverter.GetBytes(checksum).CopyTo(header, 2);
        header.CopyTo(packet, 0);
        data.CopyTo(packet, 8);
        return packet;
    }

    private static ushort CalcChecksum(byte[] header, byte[] data)
    {
        uint sum = 0;
        foreach (var b in header) sum += b;
        foreach (var b in data)   sum += b;
        while (sum >> 16 != 0) sum = (sum & 0xFFFF) + (sum >> 16);
        return (ushort)(~sum & 0xFFFF);
    }

    private static async Task<byte[]> ReadPacketAsync(NetworkStream stream, int length, CancellationToken ct)
    {
        var buf    = new byte[Math.Max(length, 1)];
        int read   = 0;
        while (read < buf.Length)
        {
            var n = await stream.ReadAsync(buf.AsMemory(read, buf.Length - read), ct).ConfigureAwait(false);
            if (n == 0) break;
            read += n;
        }
        return buf;
    }

    private static byte[] BuildAttLogRequest(DateTime from, DateTime to)
    {
        // ZKTeco encodes dates as "YYYY-MM-DD HH:MM:SS\0"
        var fromStr = from.ToString("yyyy-MM-dd HH:mm:ss") + "\0";
        var toStr   = to.ToString("yyyy-MM-dd HH:mm:ss")   + "\0";
        var bytes   = new byte[Encoding.ASCII.GetByteCount(fromStr) + Encoding.ASCII.GetByteCount(toStr)];
        var offset  = Encoding.ASCII.GetBytes(fromStr, bytes);
        Encoding.ASCII.GetBytes(toStr, bytes.AsSpan(offset));
        return bytes;
    }

    private static IReadOnlyList<BiometricPunchLog> ParseAttLogs(byte[] raw)
    {
        if (raw.Length < ATT_RECORD_SIZE) return Array.Empty<BiometricPunchLog>();

        var logs = new List<BiometricPunchLog>(raw.Length / ATT_RECORD_SIZE);
        for (int i = 0; i + ATT_RECORD_SIZE <= raw.Length; i += ATT_RECORD_SIZE)
        {
            try
            {
                // ZKTeco v2 record layout (40 bytes):
                // [0-8]   UID (null-padded ASCII)
                // [8-14]  timestamp encoded as packed BCD or binary
                // [14]    verify type
                // [15]    in/out direction (0=in, 1=out, 4=break-out, 5=break-in)
                // [26-40] work code / reserved
                var userId = Encoding.ASCII.GetString(raw, i, 9).TrimEnd('\0').Trim();

                // Bytes 8-13: YY MM DD hh mm ss (BCD-encoded)
                int year   = raw[i + 8]  + 2000;
                int month  = raw[i + 9];
                int day    = raw[i + 10];
                int hour   = raw[i + 11];
                int minute = raw[i + 12];
                int second = raw[i + 13];

                if (month < 1 || month > 12 || day < 1 || day > 31) continue;
                var punched = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);

                var direction = raw[i + 15] switch
                {
                    0 => PunchDirection.CheckIn,
                    1 => PunchDirection.CheckOut,
                    _ => PunchDirection.Unknown
                };

                if (!string.IsNullOrWhiteSpace(userId))
                    logs.Add(new BiometricPunchLog(userId, punched, direction, null));
            }
            catch { /* skip malformed record */ }
        }

        return logs;
    }

    // ── Circuit breaker helpers ───────────────────────────────────────────────

    private bool IsCircuitOpen()
    {
        lock (_cbLock) return _consecutiveFailures >= MaxConsecutiveFailures
                           && DateTime.UtcNow < _circuitOpenUntil;
    }

    private void RecordFailure()
    {
        lock (_cbLock)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= MaxConsecutiveFailures)
                _circuitOpenUntil = DateTime.UtcNow.AddSeconds(CircuitOpenDurationSeconds);
        }
    }

    private void ResetCircuit()
    {
        lock (_cbLock) { _consecutiveFailures = 0; _circuitOpenUntil = DateTime.MinValue; }
    }
}
