using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;

public static class NetUtils
{
    public static IPAddress GetBroadcastAddress(IPAddress address, IPAddress subnetMask)
    {
        var broadcastBytes = address.GetAddressBytes();
        var maskBytes = subnetMask.GetAddressBytes();
        for (var i = 0; i < maskBytes.Length; ++i)
            broadcastBytes[i] |= unchecked((byte)~maskBytes[i]);
        return new IPAddress(broadcastBytes);
    }

    public static IEnumerable<NetworkInterface> GetNetworkInterfaces()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic =>
            nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
            nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
            nic.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet);
    }

    public static IPAddress ResolveHostName(string hostname)
    {
        return Dns.GetHostAddresses(hostname).FirstOrDefault();
    }

    public static IPAddress GetLocalIPAddress(IPAddress destinationIPAddress)
    {
        var destinationAddressBytes = destinationIPAddress.GetAddressBytes();
        foreach (var nic in GetNetworkInterfaces())
        {
            // make sure the interface is up:
            if (nic.OperationalStatus != OperationalStatus.Up)
                continue;
            // make sure that the interface is a Wifi of an Ethernet NIC:
            switch (nic.NetworkInterfaceType)
            {
                case NetworkInterfaceType.Wireless80211:
                case NetworkInterfaceType.Ethernet:
                case NetworkInterfaceType.GigabitEthernet:
                    break;
                default:
                    continue;
            }

            foreach (var addr in nic.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily != destinationIPAddress.AddressFamily)
                    continue;
                var maskBytes = addr.IPv4Mask.GetAddressBytes();
                var addrBytes = addr.Address.GetAddressBytes();
                var found = true;
                for (int i = 0; i < addrBytes.Length; ++i)
                {
                    if ((addrBytes[i] & maskBytes[i]) != (destinationAddressBytes[i] & maskBytes[i]))
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                    return addr.Address;
            }
        }
        return null;
    }
}