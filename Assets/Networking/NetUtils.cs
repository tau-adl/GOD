using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

public static class NetUtils
{
    public static int CompareTo(IPAddress a, IPAddress b)
    {
        var bytesA = a.GetAddressBytes();
        var bytesB = b.GetAddressBytes();
        if (bytesA.Length != bytesB.Length)
        {
            // address lengths are different.
            return bytesA.Length.CompareTo(bytesB.Length);
        }
        for (int i = 0; i < bytesA.Length; ++i)
        {
            var compare = bytesA[i].CompareTo(bytesB[i]);
            if (compare != 0)
                return compare;
        }
        return 0; // the addresses are identical.
    }

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

    private static bool SameSubnet(byte[] addressA, byte[] addressB, byte[] subnetMask)
    {
        if (addressA == null)
            throw new ArgumentNullException(nameof(addressA));
        if (addressB == null)
            throw new ArgumentNullException(nameof(addressB));
        if (subnetMask == null)
            throw new ArgumentNullException(nameof(subnetMask));
        if (addressA.Length != addressB.Length || addressA.Length != subnetMask.Length)
            throw new ArgumentException("All arguments must be byte-arrays of the same length.");
        for (var i = 0; i < addressA.Length; ++i)
        {
            if ((addressA[i] & subnetMask[i]) != (addressB[i] & subnetMask[i]))
                return false;
        }

        return true;
    }

    public static Tuple<NetworkInterface, UnicastIPAddressInformation> GetLocalIPAddressInformationFromRemoteAddress(IPAddress remoteIPAddress)
    {
        if (remoteIPAddress == null)
            throw new ArgumentNullException(nameof(remoteIPAddress));

        var remoteAddressBytes = remoteIPAddress.GetAddressBytes();
        return (
            from nic in NetworkInterface.GetAllNetworkInterfaces()
            from info in nic.GetIPProperties().UnicastAddresses
            where info.Address.AddressFamily == remoteIPAddress.AddressFamily
            let localAddressBytes = info.Address.GetAddressBytes()
            let subnetMaskBytes = info.IPv4Mask.GetAddressBytes()
            where SameSubnet(localAddressBytes, remoteAddressBytes, subnetMaskBytes)
            orderby nic.OperationalStatus == OperationalStatus.Up descending
            select Tuple.Create(nic, info)).FirstOrDefault();
        // Assumption: If two NICs share the same IP address, at least one of
        //             then must be down.
    }

    public static IPAddress GetLocalIPAddress(IPAddress remoteIPAddress)
    {
        var info = GetLocalIPAddressInformationFromRemoteAddress(remoteIPAddress);
        return info != null && info.Item1.OperationalStatus == OperationalStatus.Up
            ? info.Item2.Address
            : null;
    }

    public static bool IsAny(IPAddress address)
    {
        if (address == null)
            throw new ArgumentNullException(nameof(address));
        switch (address.AddressFamily)
        {
            case AddressFamily.InterNetwork:
                return IPAddress.Any.Equals(address);
            case AddressFamily.InterNetworkV6:
                return IPAddress.IPv6Any.Equals(address);
            default:
                throw new ArgumentException("Only IPv4 and IPv6 addresses are supported.", nameof(address));
        }
    }

    public static Tuple<NetworkInterface, UnicastIPAddressInformation> GetLocalIPAddressInformation(IPAddress localIPAddress, IPAddress remoteIPAddress)
    {
        if (localIPAddress == null)
            throw new ArgumentNullException(nameof(localIPAddress));
        if (IsAny(localIPAddress))
            return GetLocalIPAddressInformationFromRemoteAddress(remoteIPAddress);
        if (remoteIPAddress == null)
            throw new ArgumentNullException(nameof(remoteIPAddress));
        if (localIPAddress.AddressFamily != remoteIPAddress.AddressFamily)
            throw new ArgumentException("local and remote IP addresses must be of the same family.");

        var localAddressBytes = localIPAddress.GetAddressBytes();
        var remoteAddressBytes = remoteIPAddress.GetAddressBytes();
        return (
            from nic in NetworkInterface.GetAllNetworkInterfaces()
            from info in nic.GetIPProperties().UnicastAddresses
            where info.Address.Equals(localIPAddress)
            let subnetMaskBytes = info.IPv4Mask.GetAddressBytes()
            where SameSubnet(localAddressBytes, remoteAddressBytes, subnetMaskBytes)
            orderby nic.OperationalStatus == OperationalStatus.Up descending
            select Tuple.Create(nic, info)).FirstOrDefault();
        // Assumption: If two NICs share the same IP address, at least one of
        //             then must be down.
    }
}