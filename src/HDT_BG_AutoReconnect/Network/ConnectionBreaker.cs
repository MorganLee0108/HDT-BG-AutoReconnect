using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using BgAutoReconnect.Native;
using Hearthstone_Deck_Tracker.Utility.Logging;

namespace BgAutoReconnect.Network
{
    internal class ConnectionBreaker
    {
        private const string HearthstoneProcessName = "Hearthstone";

        public ConnectionStatus Status { get; private set; } = ConnectionStatus.Connected;

        public enum ConnectionStatus
        {
            Disconnected,
            Connected
        }

        public int Disconnect(string addr, ushort port)
        {
            var (hsTcpRow, found) = GetReconnectTcp(addr, port);
            if (!found || !IsValidTcpRow(hsTcpRow))
            {
                Log.Error($"No Hearthstone TCP connection found for {addr}:{port}");
                return 1;
            }

            hsTcpRow.state = (uint)TcpTableNative.MibTcpState.MibTcpStateDeleteTcb;

            var result = TcpTableNative.SetTcpRow(hsTcpRow);
            switch (result)
            {
                case TcpTableNative.SetTcpErrorCode.NoError:
                    Log.Info("Disconnect successfully");
                    Status = ConnectionStatus.Disconnected;
                    return 0;
                case TcpTableNative.SetTcpErrorCode.ErrorAccessDenied:
                    Log.Error("Access denied");
                    break;
                case TcpTableNative.SetTcpErrorCode.ErrorInvalidParameter:
                    Log.Error("Invalid parameter");
                    break;
                case TcpTableNative.SetTcpErrorCode.ErrorNotElevated:
                    Log.Error("Not elevated");
                    break;
                case TcpTableNative.SetTcpErrorCode.ErrorNotSupported:
                    Log.Error("Not supported");
                    break;
                default:
                    Log.Error("Other errors: " + result);
                    break;
            }

            return 1;
        }

        public void MarkConnected()
        {
            Status = ConnectionStatus.Connected;
            Log.Info("Connection restored");
        }

        private static Process[] GetHearthstoneProcesses() => Process.GetProcessesByName(HearthstoneProcessName);

        private static bool IsValidTcpRow(TcpTableNative.MibTcpRow row)
        {
            return row.localPort != null
                   && row.localPort.Length == 4
                   && row.remotePort != null
                   && row.remotePort.Length == 4;
        }

        private static TcpTableNative.MibTcpRow ToTcpRow(TcpTableNative.MibTcpRowOwnerModule row)
        {
            return new TcpTableNative.MibTcpRow
            {
                localAddr = row.localAddr,
                localPort = row.localPort,
                remoteAddr = row.remoteAddr,
                remotePort = row.remotePort
            };
        }

        private static bool FindHsTcpConnection(string addr, ushort port, out TcpTableNative.MibTcpRowOwnerModule row)
        {
            row = default;
            var tcpRows = TcpTableNative.GetAllTcpConnections();
            var hsProcesses = GetHearthstoneProcesses();
            var pidToName = new Dictionary<uint, string>();
            foreach (var process in hsProcesses)
                pidToName.Add((uint)process.Id, process.ProcessName);

            for (var i = tcpRows.Count - 1; i >= 0; i--)
            {
                if (!pidToName.ContainsKey(tcpRows[i].ProcessId))
                    continue;

                var remotePort = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(tcpRows[i].remotePort, 0));
                var remoteAddr = new IPAddress(tcpRows[i].remoteAddr).ToString();

                if (remotePort == port && remoteAddr == addr &&
                    tcpRows[i].State == TcpTableNative.MibTcpState.MibTcpStateEstab)
                {
                    row = tcpRows[i];
                    return true;
                }
            }

            return false;
        }

        private static (TcpTableNative.MibTcpRow row, bool found) GetReconnectTcp(string addr, ushort port)
        {
            if (!string.IsNullOrEmpty(addr) && port != 0 &&
                FindHsTcpConnection(addr, port, out var match))
            {
                Log.Info($"Found TCP connection in Hearthstone: {addr}:{port}");
                return (ToTcpRow(match), true);
            }

            if (!string.IsNullOrEmpty(addr) && port != 0)
                Log.Info($"Game server connection {addr}:{port} not found, using fallback");

            var tcpRows = TcpTableNative.GetAllTcpConnections();
            var hsProcesses = GetHearthstoneProcesses();
            var lastCreateTimestamp = DateTime.MinValue;
            var foundFallback = false;
            var bestMatch = default(TcpTableNative.MibTcpRowOwnerModule);
            var pids = new HashSet<uint>();
            foreach (var process in hsProcesses)
                pids.Add((uint)process.Id);

            for (var i = tcpRows.Count - 1; i >= 0; i--)
            {
                if (!pids.Contains(tcpRows[i].ProcessId))
                    continue;

                if (tcpRows[i].CreateTimestamp >= lastCreateTimestamp &&
                    tcpRows[i].State == TcpTableNative.MibTcpState.MibTcpStateEstab)
                {
                    bestMatch = tcpRows[i];
                    lastCreateTimestamp = tcpRows[i].CreateTimestamp;
                    foundFallback = true;
                }
            }

            if (!foundFallback)
            {
                Log.Error("No established Hearthstone TCP connection found");
                return (default, false);
            }

            var remotePort = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(bestMatch.remotePort, 0));
            var remoteAddr = new IPAddress(bestMatch.remoteAddr).ToString();
            Log.Info($"Using last established Hearthstone TCP connection: {remoteAddr}:{remotePort}");
            return (ToTcpRow(bestMatch), true);
        }
    }
}
