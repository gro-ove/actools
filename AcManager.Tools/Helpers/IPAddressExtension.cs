using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace AcManager.Tools.Helpers {
    // ReSharper disable once InconsistentNaming
    public static class IPAddressExtension {
        // based on http://stackoverflow.com/questions/25281099/how-to-get-the-local-ip-broadcast-address-dynamically-c-sharp
        public static IPAddress GetBroadcastAddress(this IPAddress address, IPAddress subnetMask) {
            var addressBytes = address.GetAddressBytes();
            var subnetMaskBytes = subnetMask.GetAddressBytes();

            if (addressBytes.Length != subnetMaskBytes.Length) {
                throw new ArgumentException("Lengths of IP address and subnet mask do not match.");
            }

            return new IPAddress(addressBytes.Select((x, i) => (byte)(x | subnetMaskBytes[i] ^ byte.MaxValue)).ToArray());
        }

        public static IPAddress GetBroadcastAddress(this IPAddress address) {
            return GetBroadcastAddress(address, GetSubnetMask(address));
        }

        private static NetworkInterface[] _networkInterfaces;

        public static NetworkInterface[] NetworkInterfaces => _networkInterfaces ?? (_networkInterfaces = NetworkInterface.GetAllNetworkInterfaces());

        // based on http://www.java2s.com/Code/CSharp/Network/GetSubnetMask.htm
        public static IPAddress GetSubnetMask(this IPAddress address) {
            foreach (var addressInformation in from networkInterface in NetworkInterfaces
                                               from addressInformation in networkInterface.GetIPProperties().UnicastAddresses
                                               where addressInformation.Address.AddressFamily == AddressFamily.InterNetwork &&
                                                       address.Equals(addressInformation.Address)
                                               select addressInformation) {
                return addressInformation.IPv4Mask;
            }

            throw new Exception($"Can’t find subnetmask for IP address '{address}'");
        }
    }
}
