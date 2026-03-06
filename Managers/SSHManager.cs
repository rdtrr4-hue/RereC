using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Rene.Models;
using Rere.Models;
using RSSH = Renci.SshNet;

namespace Rere.Managers
{
    // ─── SSH Manager (Singleton) ─────────────────────────────────────────────────
    public class SSHManager
    {
        public static readonly SSHManager Shared = new SSHManager();

        private Renci.SshNet.SshClient? _client;
        private bool _isConnected;
        private SSHConfig? _currentConfig;
        private CancellationTokenSource? _monitorCts;
        private readonly SemaphoreSlim _commandLock = new SemaphoreSlim(1, 1);

        // Callbacks
        public Action<byte[]>? OnData;
        public Action<Exception>? OnError;
        public Action? OnDisconnected;

        private SSHManager() { }

        // ─── Connection ──────────────────────────────────────────────────────────

        public async Task ConnectAsync(SSHConfig config)
        {
            Disconnect();
            _currentConfig = config;

            Renci.SshNet.SshClient client;

            if (config.AuthenticationMethod == SSHConfig.AuthType.Password)
            {
                if (string.IsNullOrEmpty(config.Password))
                    throw new InvalidOperationException("Password is required");

                client = new Renci.SshNet.SshClient(
                    config.Host, config.Port, config.Username,
                    config.Password);
            }
            else
            {
                if (string.IsNullOrEmpty(config.PrivateKeyPath))
                    throw new InvalidOperationException("Private key path is required");

                Renci.SshNet.PrivateKeyFile keyFile;
                if (!string.IsNullOrEmpty(config.Passphrase))
                    keyFile = new Renci.SshNet.PrivateKeyFile(config.PrivateKeyPath, config.Passphrase);
                else
                    keyFile = new Renci.SshNet.PrivateKeyFile(config.PrivateKeyPath);

                client = new Renci.SshNet.SshClient(
                    config.Host, config.Port, config.Username, keyFile);
            }

            client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);

            await Task.Run(() => client.Connect());
            _client = client;
            _isConnected = true;
        }

        public void Disconnect()
        {
            _monitorCts?.Cancel();
            _monitorCts = null;

            if (_client != null)
            {
                try { _client.Disconnect(); } catch { }
                _client.Dispose();
                _client = null;
            }

            _isConnected = false;
            OnDisconnected?.Invoke();
        }

        public async Task<bool> TestConnectionAsync(SSHConfig config)
        {
            try
            {
                await ConnectAsync(config);
                Disconnect();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ─── Command Execution ───────────────────────────────────────────────────

        public async Task<string> ExecuteAsync(string command)
        {
            await _commandLock.WaitAsync();
            try
            {
                return await ExecuteInternalAsync(command);
            }
            finally
            {
                _commandLock.Release();
            }
        }

        public Task<string> ExecuteImmediateAsync(string command)
            => ExecuteInternalAsync(command);

        private async Task<string> ExecuteInternalAsync(string command)
        {
            if (!_isConnected || _client == null)
                throw new InvalidOperationException("Not connected");

            return await Task.Run(() =>
            {
                using var cmd = _client.CreateCommand(command);
                cmd.CommandTimeout = TimeSpan.FromSeconds(30);
                return cmd.Execute();
            });
        }

        // ─── tcpdump Monitor (SSH + tcpdump) ─────────────────────────────────────

        public async Task StartTcpDumpMonitorAsync()
        {
            if (!_isConnected || _currentConfig == null)
                throw new InvalidOperationException("Not connected");

            _monitorCts = new CancellationTokenSource();
            var token = _monitorCts.Token;

            _ = Task.Run(() => RunTcpDumpStreamAsync(token), token);
            await Task.CompletedTask;
        }

        private async Task RunTcpDumpStreamAsync(CancellationToken token)
        {
            if (_client == null) return;

            var lineBuffer = new StringBuilder();

            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var shell = _client.CreateShellStream(
                        "xterm", 80, 24, 800, 600, 1024);

                    // Start tcpdump via shell
                    shell.WriteLine("sudo tcpdump -n -i wg0 udp -l 2>/dev/null");

                    var readBuf = new byte[4096];
                    while (!token.IsCancellationRequested && shell.CanRead)
                    {
                        var available = shell.Read(readBuf, 0, readBuf.Length);
                        if (available <= 0)
                        {
                            await Task.Delay(10, token);
                            continue;
                        }

                        var chunk = Encoding.UTF8.GetString(readBuf, 0, available);
                        lineBuffer.Append(chunk);

                        var full = lineBuffer.ToString();
                        var lines = full.Split('\n');

                        // Keep last incomplete line
                        lineBuffer.Clear();
                        lineBuffer.Append(lines[^1]);

                        for (int i = 0; i < lines.Length - 1; i++)
                        {
                            var line = lines[i].Trim();
                            if (!string.IsNullOrEmpty(line))
                                ParseTcpDumpLine(line);
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                        OnError?.Invoke(ex);

                    // Reconnect delay
                    await Task.Delay(3000, token);
                }
            }
        }

        // ─── Parse tcpdump line ──────────────────────────────────────────────────

        private void ParseTcpDumpLine(string line)
        {
            // Pattern: "12:34:56.789 IP 203.12.45.88.51820 > 10.0.0.1.51820: UDP"
            var srcMatch = Regex.Match(line, @"IP\s+(\d+\.\d+\.\d+\.\d+)\.(\d+)\s+>");
            if (!srcMatch.Success) return;

            var ip = srcMatch.Groups[1].Value;
            var port = srcMatch.Groups[2].Value;

            // If source is internal, try destination
            if (ip.StartsWith("10.") || ip.StartsWith("192.168."))
            {
                var dstMatch = Regex.Match(line, @">\s+(\d+\.\d+\.\d+\.\d+)\.(\d+)");
                if (!dstMatch.Success) return;
                ip = dstMatch.Groups[1].Value;
                port = dstMatch.Groups[2].Value;
            }

            // Ignore internal / DNS / own IP
            var myIp = _currentConfig?.Host ?? "";
            if (ip.StartsWith("10.") || ip.StartsWith("192.168.") ||
                ip == "8.8.8.8" || ip == "8.8.4.4" ||
                ip == "1.0.0.1" || ip.StartsWith("1.1.1.") ||
                ip == myIp) return;

            var packet = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
            {
                ip,
                port,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            OnData?.Invoke(packet);
        }

        // ─── GeoIP Lookup ────────────────────────────────────────────────────────

        public async Task<GeoInfo> GetGeoInfoAsync(string ip)
        {
            var batchCmd = $@"
printf ""COUNTRY:""; (mmdblookup --file /root/GeoLite2-City.mmdb --ip {ip} country iso_code 2>/dev/null | grep '""' | head -1 | sed 's/.*""\(.*\)"".*/\1/'; \
mmdblookup --file /root/GeoLite2-City.mmdb --ip {ip} registered_country iso_code 2>/dev/null | grep '""' | head -1 | sed 's/.*""\(.*\)"".*/\1/') | grep -v '^$' | head -1; \
printf ""CITY:""; (mmdblookup --file /root/GeoLite2-City.mmdb --ip {ip} city names en 2>/dev/null | grep '""' | head -1 | sed 's/.*""\(.*\)"".*/\1/'; \
mmdblookup --file /root/GeoLite2-City.mmdb --ip {ip} subdivisions 0 names en 2>/dev/null | grep '""' | head -1 | sed 's/.*""\(.*\)"".*/\1/') | grep -v '^$' | head -1; \
printf ""ISP:""; (mmdblookup --file /root/GeoLite2-ASN.mmdb --ip {ip} autonomous_system_organization 2>/dev/null | grep '""' | head -1 | sed 's/.*""\(.*\)"".*/\1/'; \
mmdblookup --file /root/GeoLite2-ASN.mmdb --ip {ip} autonomous_system_number 2>/dev/null | grep -oP '[0-9]+' | head -1 | sed 's/.*/AS&/') | grep -v '^$' | head -1";

            var raw = await ExecuteAsync(batchCmd);

            string Extract(string label)
            {
                var m = Regex.Match(raw, $@"{label}:([^\n]*)", RegexOptions.IgnoreCase);
                return m.Success ? m.Groups[1].Value.Trim() : "";
            }

            var country = Extract("COUNTRY");
            var city    = Extract("CITY");
            var isp     = Extract("ISP");

            return new GeoInfo
            {
                Country = string.IsNullOrEmpty(country) ? "xx" : country,
                City    = string.IsNullOrEmpty(city) ? "Unknown" : city,
                Isp     = string.IsNullOrEmpty(isp) ? "Unknown ISP" : isp
            };
        }

        // ─── Player Database ─────────────────────────────────────────────────────

        public async Task<Dictionary<string, PlayerInfo>> LoadPlayerDatabaseAsync()
        {
            var output = await ExecuteAsync("cat /root/.gta_players.db 2>/dev/null || echo \"\"");
            var players = new Dictionary<string, PlayerInfo>();

            foreach (var rawLine in output.Split('\n'))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || !line.Contains('|')) continue;

                var parts = line.Split('|');
                if (parts.Length < 2) continue;

                var ip   = parts[0].Trim();
                var name = parts[1].Trim();
                if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(name)) continue;

                players[ip] = new PlayerInfo
                {
                    Name   = name,
                    City   = parts.Length > 2 ? parts[2].Trim() : "",
                    Isp    = parts.Length > 3 ? parts[3].Trim() : "",
                    Port   = parts.Length > 4 ? parts[4].Trim() : "",
                    Prefix = parts.Length > 5 ? parts[5].Trim() : ""
                };
            }

            return players;
        }

        // ─── WireGuard Status ────────────────────────────────────────────────────

        public Task<string> GetWireGuardStatusAsync()
            => ExecuteAsync("sudo wg show all dump");

        // ─── System Information ──────────────────────────────────────────────────

        public async Task<(string Hostname, string Uptime, string WgVersion)> GetSystemInfoAsync()
        {
            var hostname  = (await ExecuteAsync("hostname")).Trim();
            var uptime    = (await ExecuteAsync("uptime -p")).Trim();
            var wgVersion = (await ExecuteAsync("wg --version")).Trim();
            return (hostname, uptime, wgVersion);
        }

        // ─── Player Management ───────────────────────────────────────────────────

        public Task<string> SearchPlayersAsync(string name)
            => ExecuteAsync($"grep -i '{name}' /root/.gta_players.db 2>/dev/null || echo ''");

        public async Task<(bool Success, int Count, string? Error)> DeletePlayerAsync(string query)
        {
            var command = $@"
COUNT=$(grep -c '{query}' /root/.gta_players.db 2>/dev/null || echo 0)
if [ ""$COUNT"" -gt 0 ]; then
    sed -i '/{query}/d' /root/.gta_players.db
    echo ""SUCCESS:$COUNT""
else
    echo ""ERROR:Not found""
fi";
            var result = await ExecuteAsync(command);

            if (result.StartsWith("SUCCESS:"))
            {
                var countStr = result.Replace("SUCCESS:", "").Trim();
                return (true, int.TryParse(countStr, out var c) ? c : 0, null);
            }
            else if (result.StartsWith("ERROR:"))
            {
                return (false, 0, result.Replace("ERROR:", "").Trim());
            }
            return (false, 0, "Unknown error");
        }

        public async Task<(bool Success, string? Error)> AddPlayerAsync(string line)
        {
            var result = await ExecuteAsync($"echo '{line}' >> /root/.gta_players.db && echo 'SUCCESS' || echo 'ERROR'");
            return result.Contains("SUCCESS") ? (true, null) : (false, "Failed to add player");
        }

        public bool IsConnected => _isConnected;
        public SSHConfig? CurrentConfig => _currentConfig;
    }
}
