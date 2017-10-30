using System;
using System.Collections.Generic;
using ProtocolHeaderDefinition;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Threading;

namespace OpenTraceRT {
    public class RawSocketPingSerialized {

        public Socket pingSocket;
        public AddressFamily pingFamily;

        public int pingTtl;
        public ushort pingId;
        public ushort pingSequence;

        public int pingPayloadLength;
        public int pingCount;
        public int pingReceiveTimeout;

        public IPEndPoint destEndPoint;
        public IPEndPoint responseEndPoint;
        public EndPoint castResponseEndPoint;

        private byte[] pingPacket;
        private byte[] pingPayload;
        private byte[] receiveBuffer;

        private IcmpHeader icmpHeader;
        private Icmpv6Header icmpv6Header;
        private Icmpv6EchoRequest icmpv6EchoRequestHeader;
        private ArrayList protocolHeaderList;
        private DateTime pingSentTime;

        private string latency;
        private bool IsTimedOut = false;


        //    this ping class can be disposed
        /// <summary>
        /// Base constructor that initializes the member variables to default values. It also
        /// creates the events used and initializes the async callback function.
        /// </summary>
        public RawSocketPingSerialized() {

            pingSocket = null;
            pingFamily = AddressFamily.InterNetwork;
            pingTtl = 8;
            pingPayloadLength = 8;
            pingSequence = 0;
            pingReceiveTimeout = 15000;
            destEndPoint = new IPEndPoint(IPAddress.Loopback, 0);
            protocolHeaderList = new ArrayList();
            icmpHeader = null;
            icmpv6Header = null;
            icmpv6EchoRequestHeader = null;
        }

        /// <summary>
        /// Constructor that overrides several members of the ping packet such as TTL,
        /// payload length, ping ID, etc.
        /// </summary>
        /// 
        /// <param name="af">Indicates whether we're doing IPv4 or IPv6 ping</param>
        /// <param name="ttlValue">Time-to-live value to set on ping packet</param>
        /// <param name="payloadSize">Number of bytes in ping payload</param>
        /// <param name="sendCount">Number of times to send a ping request</param>
        /// <param name="idValue">ID value to put into ping header</param>
        public RawSocketPingSerialized(

            AddressFamily af,
            int ttlValue,
            int payloadSize,
            int sendCount,
            ushort idValue

            ) : this() {

            pingFamily = af;
            pingTtl = ttlValue;
            pingPayloadLength = payloadSize;
            pingCount = sendCount;
            pingId = idValue;
        }

        public void Close() {

            try {

                // Close the socket handle which will cause any async operations on it to complete with an error.
                //Console.WriteLine("In closing method...");
                if (pingSocket != null) {
                    ;
                    while (!IsTimedOut) {
                        if (DateTime.Now.AddMilliseconds(pingReceiveTimeout) < DateTime.Now) {
                            IsTimedOut = true;
                        }
                        GetReply();
                        Thread.Sleep(500);
                    }

                    Console.WriteLine("Closing");
                    pingSocket.Close();
                    pingSocket = null;
                }
            }

            catch (Exception err) {

                Console.WriteLine("Error occurred during cleanup: {0}", err.Message);
                throw;
            }
        }

        public void GetReply() {

            TimeSpan elapsedTime;
            IPAddress receivedAddress;
            int bytesReceived = 0;
            ushort receivedId = 0;

            //Console.WriteLine("Fetching reply");
            try {

                // Complete the receive op by calling EndReceiveFrom. This will return the number
                // of bytes received as well as the source address of who sent this packet.
                if (pingSocket != null) {
                    bytesReceived = pingSocket.ReceiveFrom(receiveBuffer, ref castResponseEndPoint);
                }

                // Calculate the elapsed time from when the ping request was sent and a response was
                // received.
                
                responseEndPoint = (IPEndPoint)castResponseEndPoint;                

                // Here we unwrap the data received back into the respective protocol headers such
                // that we can find the ICMP ID in the ICMP or ICMPv6 packet to verify that
                // the echo response we received was really a response to our request.
                if (pingSocket.AddressFamily == AddressFamily.InterNetwork) {

                    Ipv4Header v4Header;
                    IcmpHeader icmpv4Header;
                    byte[] pktIcmp;
                    int offset = 0;

                    // Remember, raw IPv4 sockets will return the IPv4 header along with all
                    // subsequent protocol headers
                    v4Header = Ipv4Header.Create(receiveBuffer, ref offset);
                    pktIcmp = new byte[bytesReceived - offset];
                    Array.Copy(receiveBuffer, offset, pktIcmp, 0, pktIcmp.Length);
                    icmpv4Header = IcmpHeader.Create(pktIcmp, ref offset);

                    receivedAddress = v4Header.SourceAddress;
                    receivedId = icmpv4Header.Id;

                    if (receivedId == pingId && destEndPoint.Address.Equals(receivedAddress)) {
                        elapsedTime = DateTime.Now - pingSentTime;
                        latency = elapsedTime.Milliseconds.ToString();
                        Console.WriteLine("writing out latency " + latency);
                    }
                }

                else if (pingSocket.AddressFamily == AddressFamily.InterNetworkV6) {

                    Icmpv6Header icmp6Header;
                    Icmpv6EchoRequest echoHeader;
                    byte[] pktEchoRequest;
                    int offset = 0;

                    // For IPv6 raw sockets, the IPv6 header is never returned along with the
                    // data received -- the received data always starts with the header
                    // following the IPv6 header.
                    icmp6Header = Icmpv6Header.Create(receiveBuffer, ref offset);
                    pktEchoRequest = new byte[bytesReceived - offset];
                    Array.Copy(receiveBuffer, offset, pktEchoRequest, 0, pktEchoRequest.Length);
                    echoHeader = Icmpv6EchoRequest.Create(pktEchoRequest, ref offset);

                    receivedAddress = icmp6Header.ipv6Header.SourceAddress;
                    receivedId = echoHeader.Id;

                    if (receivedId == pingId && destEndPoint.Address.Equals(receivedAddress)) {
                        elapsedTime = DateTime.Now - pingSentTime;
                        latency = elapsedTime.Milliseconds.ToString();
                        Console.WriteLine("writing out latency " + latency);
                    }
                }

           
            }

            catch (SocketException err) {
                Console.WriteLine("Socket error occurred in async callback: {0}", err.Message);
            }
        }

        /// <summary>
        /// Since ICMP raw sockets don't care about the port (as the ICMP protocol has no port
        /// field), we require the caller to just update the IPAddress of the destination
        /// although internally we keep it as an IPEndPoint since the SendTo method requires
        /// that (and the port is simply set to zero).
        /// </summary>
        public IPAddress PingAddress {
            get {

                return destEndPoint.Address;
            }
            set {

                destEndPoint = new IPEndPoint(value, 0);
            }
        }

        public void InitializeSocket() {

            //Console.WriteLine("Init socket");
            IPEndPoint localEndPoint;

            if (destEndPoint.AddressFamily == AddressFamily.InterNetwork) {

                // Create the raw socket
                pingSocket = new Socket(destEndPoint.AddressFamily, SocketType.Raw, ProtocolType.Icmp);
                localEndPoint = new IPEndPoint(IPAddress.Any, 0);

                // Socket must be bound locally before socket options can be applied
                pingSocket.Bind(localEndPoint);

                pingSocket.SetSocketOption(

                    SocketOptionLevel.IP,
                    SocketOptionName.IpTimeToLive,
                    pingTtl
                    );

                // Allocate the buffer used to receive the response
                receiveBuffer = new byte[Ipv4Header.Ipv4HeaderLength + IcmpHeader.IcmpHeaderLength + pingPayloadLength];
                responseEndPoint = new IPEndPoint(destEndPoint.Address, 0);
                castResponseEndPoint = (EndPoint)responseEndPoint;
            }

            else if (destEndPoint.AddressFamily == AddressFamily.InterNetworkV6) {

                // Create the raw socket
                pingSocket = new Socket(
                    destEndPoint.AddressFamily,
                    SocketType.Raw,
                    (ProtocolType)58       // ICMPv6 protocol value
                    );

                localEndPoint = new IPEndPoint(IPAddress.IPv6Any, 0);

                // Socket must be bound locally before socket options can be applied
                pingSocket.Bind(localEndPoint);

                pingSocket.SetSocketOption(

                    SocketOptionLevel.IPv6,
                    SocketOptionName.IpTimeToLive,
                    pingTtl
                    );

                // Allocate the buffer used to receive the response
                receiveBuffer = new byte[Ipv6Header.Ipv6HeaderLength +
                    Icmpv6Header.Icmpv6HeaderLength + Icmpv6EchoRequest.Icmpv6EchoRequestLength +
                    pingPayloadLength];

                responseEndPoint = new IPEndPoint(IPAddress.IPv6Any, 0);
                castResponseEndPoint = (EndPoint)responseEndPoint;

            }

        }



        public void BuildPingPacket() {

            //Console.WriteLine("Building packet");

            if (pingSocket == null) {

                InitializeSocket();
            }

            protocolHeaderList.Clear();

            if (destEndPoint.AddressFamily == AddressFamily.InterNetwork) {

                icmpHeader = new IcmpHeader();
                icmpHeader.Id = pingId;
                icmpHeader.Sequence = pingSequence;
                icmpHeader.Type = IcmpHeader.EchoRequestType;
                icmpHeader.Code = IcmpHeader.EchoRequestCode;

                pingPayload = new byte[pingPayloadLength];

                for (int i = 0; i < pingPayload.Length; i++) {

                    pingPayload[i] = (byte)'e';
                }

                protocolHeaderList.Add(icmpHeader);
            }
            else if (destEndPoint.AddressFamily == AddressFamily.InterNetworkV6) {

                Ipv6Header ipv6Header;
                IPEndPoint localInterface;
                byte[] localAddressBytes = new byte[28];

                ipv6Header = new Ipv6Header();

                pingSocket.IOControl(

                    WinsockIoctl.SIO_ROUTING_INTERFACE_QUERY,
                    SockaddrConvert.GetSockaddrBytes(destEndPoint),
                    localAddressBytes
                    );

                localInterface = SockaddrConvert.GetEndPoint(localAddressBytes);

                ipv6Header.SourceAddress = localInterface.Address;
                ipv6Header.DestinationAddress = destEndPoint.Address;
                ipv6Header.NextHeader = 58;

                // Initialize the ICMPv6 header
                icmpv6Header = new Icmpv6Header(ipv6Header);
                icmpv6Header.Type = Icmpv6Header.Icmpv6EchoRequestType;
                icmpv6Header.Code = Icmpv6Header.Icmpv6EchoRequestCode;

                for (int i = 0; i < pingPayload.Length; i++) {

                    pingPayload[i] = (byte)'e';
                }

                // Create the ICMPv6 echo request header
                icmpv6EchoRequestHeader = new Icmpv6EchoRequest();
                icmpv6EchoRequestHeader.Id = pingId;

                // Add the headers to the protocol header list
                protocolHeaderList.Add(icmpv6Header);
                protocolHeaderList.Add(icmpv6EchoRequestHeader);
            }
        }


        public string DoPing(int interval) {

            if (protocolHeaderList.Count == 0) {

                BuildPingPacket();
            }

            try {

                while (pingCount > 0) {

                    Interlocked.Decrement(ref pingCount);

                    if (destEndPoint.AddressFamily == AddressFamily.InterNetwork) {

                        icmpHeader.Sequence = (ushort)(icmpHeader.Sequence + (ushort)1);
                        pingPacket = icmpHeader.BuildPacket(protocolHeaderList, pingPayload);
                    }
                    else if (destEndPoint.AddressFamily == AddressFamily.InterNetworkV6) {
                        icmpv6EchoRequestHeader.Sequence = (ushort)(icmpv6EchoRequestHeader.Sequence + (ushort)1);
                        pingPayload = icmpv6Header.BuildPacket(protocolHeaderList, pingPayload);
                    }

                    pingSentTime = DateTime.Now;
                    pingSocket.SendTo(pingPacket, destEndPoint);

                    Thread.Sleep(interval);                                      
                }

                if (String.IsNullOrEmpty(latency)) {
                    Thread.Sleep(2000);
                }

                return latency;
            }
            catch (SocketException err) {

                Console.WriteLine("Socket error in DoPing(): {0}", err.Message);
                throw;
            }
        }
    }
}
