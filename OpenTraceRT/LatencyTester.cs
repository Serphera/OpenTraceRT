using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Threading;
using System.Diagnostics;

namespace OpenTraceRT {
    class LatencyTester {

        public DataItem MakeRequest(string hostName, CancellationToken token) {      

            int sendCount = 2;
            int timeout = 900;
            int isTimeout = 3;

            List<int> packetList = new List<int>();
            List<int?> latencyList = new List<int?>();

            byte[] buffer = new byte[32];

            for (int i = 0; i < sendCount; i++) {

                Stopwatch sw = new Stopwatch();

                Ping pinger = new Ping();

                PingOptions options = new PingOptions();
                options.Ttl = 128;

                PingReply reply = pinger.Send(hostName, timeout);
                sw.Start();

                while (timeout > sw.ElapsedMilliseconds && isTimeout > 0) {
                    if (token.IsCancellationRequested) {
                        return null;
                    }

                    if (reply.Status == IPStatus.Success) {

                        latencyList.Add(Convert.ToInt16(reply.RoundtripTime) );
                        packetList.Add(reply.Buffer.Length);
                        break;
                    }
                    if (reply.Status == IPStatus.TimedOut) {

                        latencyList.Add(null);
                        packetList.Add(reply.Buffer.Length);
                        isTimeout--;
                        break;
                    }

                    Thread.Sleep(50);
                }

                if (timeout < sw.ElapsedMilliseconds && isTimeout > 0) {
                    packetList.Add(0);
                    isTimeout--;
                }
                Thread.Sleep(750);
            }

            int? finalLatency = 0;
            decimal totalPacketLoss = 0;

            for (int i = 0; i < latencyList.Count; i++) {

                if (latencyList[i] > finalLatency) {
                    finalLatency = latencyList[i];
                }
                totalPacketLoss = totalPacketLoss + packetList[i];
            }

            //Calculates packet loss %
            if (totalPacketLoss != 0) {
                totalPacketLoss = 100 - ((totalPacketLoss / (32 * sendCount)) * 100);
            }
            
            string latency = "";

            if (finalLatency == 0) {

                latency = "*";
            }
            else {

                latency = finalLatency.ToString();
            }

            return new DataItem { latency = latency, hostname = hostName, packetloss = totalPacketLoss, Time = DateTime.Now };
        }
    }
}
