using MadWizard.Insomnia.Events;
using MadWizard.Insomnia.Modules.Session;
using Microsoft.WindowsAPICodePack.Net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace MadWizard.Insomnia.Modules.S3Handlers
{
    internal class S3Terminator : ModuleBase
    {
        Timer timer;

        // Config //

        bool continuous;

        string[] wakeNetworks;
        bool networkConnected;

        int wakePort;

        // Options //

        bool resolveIPAdr;

        internal S3Terminator(bool continuous = false)
        {
            this.continuous = continuous;

            Hosts = new Dictionary<string, WakeHost>();

            AddDependency(typeof(TerminalServer));
        }

        public bool IsNetworkConnected
        {
            get
            {
                return networkConnected;
            }

            private set
            {
                if (networkConnected != value)
                {
                    networkConnected = value;

                    if (Context.DebugLog)
                        EventLog.WriteEntry($"Networks connected:  {string.Join(", ", QueryNetworks())}", EventLogEntryType.Information, 32);

                    NetworkChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        public bool IsResolveIPAddress
        {
            get
            {
                return resolveIPAdr;
            }

            set
            {
                if (resolveIPAdr != value)
                {
                    resolveIPAdr = value;

                    OptionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public IDictionary<string, WakeHost> Hosts { get; private set; }

        public event EventHandler NetworkChanged;
        public event EventHandler OptionChanged;
        public event EventHandler HostsChanged;

        public void ConfigureWake(string hostname, bool wake)
        {
            lock (Hosts)
            {
                WakeHost host = Hosts[hostname];
                host.IsWaking = wake;
                Hosts[hostname] = host;

                Context.Config.WriteKey("wakeup", hostname, host.ToConfigString());
            }

            HostsChanged?.Invoke(this, EventArgs.Empty);

            WakeHosts();
        }

        #region EventHandlers
        protected override void OnActivate(ServiceContext ctx)
        {
            timer = new Timer();
            timer.AutoReset = false;
            timer.Interval = ctx.Interval;
            timer.Elapsed += OnTimerElapsed;

            Component<TerminalServer>().UserLogin += OnUserLogin;

            wakeNetworks = ctx.Config.ReadKey("s3", "wakenetwork", null)?.Split('|').Select(s => s.ToLower()).ToArray();
            networkConnected = wakeNetworks == null;
            wakePort = int.Parse(ctx.Config.ReadKey("s3", "wakeport", 1473));

            foreach (string name in ctx.Config.ListKeys("wakeup"))
            {
                string[] splitMAC = ctx.Config.ReadKey("wakeup", name).Split('|');

                string rawMAC = splitMAC[0];

                WakeHost host = new WakeHost(name, new string(rawMAC.Where(c => char.IsLetterOrDigit(c)).ToArray()));

                if (splitMAC.Length > 1)
                    if (splitMAC[1].Equals("on", StringComparison.InvariantCultureIgnoreCase))
                        host.IsWaking = true;
                    else if (splitMAC[1].Equals("off", StringComparison.InvariantCultureIgnoreCase))
                        host.IsWaking = false;
                    else throw new ArgumentOutOfRangeException(splitMAC[1]);

                Hosts[name] = host;
            }
        }
        protected override void OnEvent(ModuleBase source, EventBase e)
        {
            if (e is UserPresentEvent)
            {
                WakeHosts();
            }
        }
        protected override void OnPowerChange(PowerChangeEvent e)
        {
            if (e.Status == PowerBroadcastStatus.Suspend)
                timer.Stop();
        }
        protected override void OnStart()
        {
            WakeHosts();
        }

        private void OnUserLogin(object sender, EventArgs e)
        {
            WakeHosts();
        }
        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            WakeHosts();
        }
        #endregion

        internal void CheckNetwork()
        {
            if (wakeNetworks != null)
            {
                bool connected = false;
                foreach (Network network in NetworkListManager.GetNetworks(NetworkConnectivityLevels.Connected))
                    if (wakeNetworks.Contains(network.Name.ToLower()) || wakeNetworks.Contains(network.NetworkId.ToString().ToLower()))
                        connected = true;

                IsNetworkConnected = connected;
            }
        }

        private void WakeHosts()
        {
            CheckNetwork();

            if (IsNetworkConnected)
                lock (Hosts)
                    foreach (WakeHost host in Hosts.Values)
                        if (host.IsWaking)
                            try
                            {
                                IPAddress ip = IPAddress.Broadcast;

                                if (IsResolveIPAddress)
                                {
                                    try
                                    {
                                        ip = Dns.GetHostEntry(host.Name).AddressList.First(addr => addr.AddressFamily == AddressFamily.InterNetwork);
                                    }
                                    catch (Exception e)
                                    {
                                        EventLog.WriteEntry(e.ToString(), EventLogEntryType.Error, 39);
                                    }
                                }

                                if (Context.DebugLog)
                                    EventLog.WriteEntry($"Sending WOL -> {ip}:{wakePort} ({host.Name})", EventLogEntryType.Information, 31);

                                SendMagicPacket(host.MAC, ip, wakePort);
                            }
                            catch (Exception e)
                            {
                                EventLog.WriteEntry(e.ToString(), EventLogEntryType.Error, 39);
                            }

            if (continuous)
                timer.Start();
        }

        private static void SendMagicPacket(string MAC_ADDRESS, IPAddress ip, int port)
        {
            UdpClient udp = new UdpClient();

            try
            {
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);

                int offset = 0;
                byte[] buffer = new byte[6 + 6 * 16];

                //first 6 bytes should be 0xFF
                for (int y = 0; y < 6; y++)
                    buffer[offset++] = 0xFF;

                //now repeate MAC 16 times
                for (int y = 0; y < 16; y++)
                {
                    int i = 0;
                    for (int z = 0; z < 6; z++)
                    {
                        buffer[offset++] = byte.Parse(MAC_ADDRESS.Substring(i, 2), NumberStyles.HexNumber);
                        i += 2;
                    }
                }

                udp.EnableBroadcast = true;
                udp.Send(buffer, buffer.Length, new IPEndPoint(ip, port));
            }
            finally
            {
                udp.Close();
            }
        }

        private static string[] QueryNetworks()
        {
            return NetworkListManager.GetNetworks(NetworkConnectivityLevels.Connected).Select(network => network.Name).ToArray();
        }
    }

    [Serializable]
    public struct WakeHost
    {
        public string Name { get; private set; }
        public string MAC { get; private set; }

        public bool IsWaking { get; set; }

        internal WakeHost(string name, string mac)
        {
            Name = name;
            MAC = mac;

            IsWaking = true;
        }

        public string ToConfigString()
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < MAC.Length; i++)
            {
                if (i > 0 && i % 2 == 0)
                    sb.Append(':');

                sb.Append(MAC[i]);
            }

            if (!IsWaking)
                sb.Append("|off");

            return sb.ToString();
        }

        public override string ToString()
        {
            return $"WakeHost[Name={Name}, IsWaking={IsWaking}]";
        }
    }
}