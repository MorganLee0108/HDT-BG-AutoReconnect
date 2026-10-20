using System;
using System.Text.RegularExpressions;

namespace BgAutoReconnect.GameLog
{
    internal class ConnectionLogHandler
    {
        private static readonly Regex GotoGameServer = new Regex(
            @"Network\.GotoGameServ\w*.*address=[ ]*(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}):(\d{1,5})",
            RegexOptions.Compiled);

        public void Handle(string line, ReconnectOverlay overlay)
        {
            var match = GotoGameServer.Match(line);
            if (!match.Success)
                return;

            var addr = match.Groups[1].Value.Trim();
            var port = ushort.Parse(match.Groups[2].Value);
            overlay.OnServerAddressUpdated(addr, port);
        }
    }
}
