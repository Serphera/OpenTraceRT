using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace OpenTraceRT {
    class DataItem : ICloneable {

        public string jumps { get; set; }
        public string latency { get; set; }
        public string hostname { get; set; }
        public decimal packetloss { get; set; }

        public DateTime Time { get; set; }

        public object Clone() {
            string jump = this.jumps;
            string ltc = this.latency;
            string host = this.hostname;
            decimal packetL = this.packetloss;
            DateTime time = this.Time;
            object clone = new DataItem { jumps = jump, latency = ltc, hostname = host, packetloss = packetL, Time = time };
            return clone;
        }
    }
}
