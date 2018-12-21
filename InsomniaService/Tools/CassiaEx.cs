using Cassia;
using System;
using System.Runtime.InteropServices;

namespace MadWizard.Insomnia.Tools
{
    static class CassiaEx
    {
        [DllImport("Kernel32.dll")]
        static extern UInt32 WTSGetActiveConsoleSessionId();

        public static bool IsRemoteConnected(this ITerminalServicesSession session)
        {
            return session.ClientName.Length > 0;
        }

        public static string GetClientUser(this ITerminalServicesSession session)
        {
            string user = session.ClientName;
            if (session.ClientName.Length > 0 && session.UserName.Length > 0)
                user += "\\";
            user += session.UserName;
            return user;
        }

        public static ITerminalServicesSession GetConsoleSession(this ITerminalServer server)
        {
            return server.GetSession((int)WTSGetActiveConsoleSessionId());
        }
    }
}