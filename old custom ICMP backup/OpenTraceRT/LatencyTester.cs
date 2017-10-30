using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace OpenTraceRT {
    class LatencyTester {
        public DataItem MakeRequest(string remoteHost, int addressType, int dataSize, int ttlValue) {

            System.Diagnostics.Process proc;
            RawSocketPingSerialized pingSocket = null;
            //RawSocketPing pingSocket = null;
            AddressFamily addressFamily = AddressFamily.Unspecified;
            IPAddress remoteAddress = null;
            int sendCount = 1;

            // Force to ping over IPv4 or IPv6				
            if (addressType == 4) {
                addressFamily = AddressFamily.InterNetwork;
            }
            else if (addressType == 6) {
                addressFamily = AddressFamily.InterNetworkV6;
            }
            else {
                return null;
            }

            // Try to resolve the address or name passed
            try {
                IPAddress[] hostEntry = Dns.GetHostAddresses(remoteHost);

                foreach (IPAddress addr in hostEntry) {
                    remoteAddress = addr;

                    if (remoteAddress.AddressFamily == addressFamily) {

                        break;
                    }
                    remoteAddress = null;
                }

                if (remoteAddress == null) {

                    Console.WriteLine("Ping request could not find host {0}. Please check the name and try again", remoteHost);
                    return null;
                }
            }
            catch (SocketException err) {

                Console.WriteLine("Bad name {0}: {1}", remoteHost, err.Message);
                return null;
            }

            String latencyList = "";
            // Get our process ID which we'll use in the ICMP ID field
            proc = System.Diagnostics.Process.GetCurrentProcess();

            try {
                // Create a RawSocketPing class that wraps all the ping functionality
                pingSocket = new RawSocketPingSerialized(
                    remoteAddress.AddressFamily,
                    ttlValue,
                    dataSize,
                    sendCount,
                    (ushort)proc.Id
                    );


                pingSocket.PingAddress = remoteAddress;
                pingSocket.InitializeSocket();

                // Create the ICMP packets to send
                pingSocket.BuildPingPacket();

                // Actually send the ping request and wait for response
                latencyList = pingSocket.DoPing(1000);
            }
            catch (SocketException err) {
                Console.WriteLine("Socket error occurred: {0}", err.Message);
            }
            finally {
                //Thread.Sleep(1000);
                if (pingSocket != null)
                    pingSocket.Close();
            }

            return new DataItem { hostname = remoteHost,  latency = latencyList};
        }
    }
}
