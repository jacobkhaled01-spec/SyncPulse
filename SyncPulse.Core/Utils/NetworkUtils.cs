using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SyncPulse.Core.Utils
{
    /// <summary>
    /// أدوات استكشاف بطاقات الشبكة (Wi-Fi & Ethernet Network Utilities)
    /// </summary>
    public static class NetworkUtils
    {
        /// <summary>
        /// جلب عناوين IPv4 المحلية لبطاقات الشبكة النشطة (WLAN & Ethernet)
        /// </summary>
        public static List<IPAddress> GetLocalIPv4Addresses()
        {
            var addresses = new List<IPAddress>();
            foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (netInterface.OperationalStatus != OperationalStatus.Up ||
                    netInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var ipProps = netInterface.GetIPProperties();
                foreach (var addr in ipProps.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(addr.Address))
                    {
                        addresses.Add(addr.Address);
                    }
                }
            }

            if (!addresses.Any())
            {
                addresses.Add(IPAddress.Loopback);
            }

            return addresses;
        }

        public static string GetPrimaryLocalIP()
        {
            var ips = GetLocalIPv4Addresses();
            return ips.FirstOrDefault()?.ToString() ?? "127.0.0.1";
        }
    }
}
