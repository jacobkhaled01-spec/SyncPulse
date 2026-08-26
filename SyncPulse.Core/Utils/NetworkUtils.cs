using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SyncPulse.Core.Utils
{
    /// <summary>
    /// أدوات استكشاف بطاقات الشبكة وحساب عناوين البث التلقائي (Wi-Fi & Ethernet Network Utilities)
    /// </summary>
    public static class NetworkUtils
    {
        /// <summary>
        /// جلب عناوين IPv4 المحلية لبطاقات الشبكة النشطة مع استبعاد البطاقات الوهمية
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

                string name = netInterface.Name.ToLowerInvariant();
                string desc = netInterface.Description.ToLowerInvariant();
                if (name.Contains("virtual") || desc.Contains("virtual") ||
                    name.Contains("vethernet") || desc.Contains("hyper-v") ||
                    name.Contains("wsl") || name.Contains("bluetooth"))
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
                // Fallback to any active interface
                foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (netInterface.OperationalStatus == OperationalStatus.Up &&
                        netInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        foreach (var addr in netInterface.GetIPProperties().UnicastAddresses)
                        {
                            if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                                !IPAddress.IsLoopback(addr.Address))
                            {
                                addresses.Add(addr.Address);
                            }
                        }
                    }
                }
            }

            if (!addresses.Any())
            {
                addresses.Add(IPAddress.Loopback);
            }

            return addresses.Distinct().ToList();
        }

        /// <summary>
        /// جلب عنوان IP الأساسي الفعلي (يفضل بطاقة الواي فاي أو الإيثرنت)
        /// </summary>
        public static string GetPrimaryLocalIP()
        {
            var ips = GetLocalIPv4Addresses();
            return ips.FirstOrDefault()?.ToString() ?? "127.0.0.1";
        }

        /// <summary>
        /// حساب كافة عناوين البث التلقائي (Broadcast Addresses) لجميع كروت الشبكة والواي فاي
        /// </summary>
        public static List<IPAddress> GetBroadcastAddresses()
        {
            var broadcastList = new List<IPAddress> { IPAddress.Broadcast };

            try
            {
                foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (netInterface.OperationalStatus != OperationalStatus.Up ||
                        netInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }

                    var ipProps = netInterface.GetIPProperties();
                    foreach (var unicast in ipProps.UnicastAddresses)
                    {
                        if (unicast.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !IPAddress.IsLoopback(unicast.Address) &&
                            unicast.IPv4Mask != null)
                        {
                            byte[] ipBytes = unicast.Address.GetAddressBytes();
                            byte[] maskBytes = unicast.IPv4Mask.GetAddressBytes();
                            byte[] broadcastBytes = new byte[ipBytes.Length];

                            for (int i = 0; i < ipBytes.Length; i++)
                            {
                                broadcastBytes[i] = (byte)(ipBytes[i] | (~maskBytes[i]));
                            }

                            broadcastList.Add(new IPAddress(broadcastBytes));
                        }
                    }
                }
            }
            catch { }

            return broadcastList.Distinct().ToList();
        }
    }
}
