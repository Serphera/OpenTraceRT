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
        public string packetloss { get; set; }
        public string PH { get; set; }
        public Canvas canvas { get; set; }

        public object Clone() {
            string jump = this.jumps;
            string ltc = this.latency;
            string host = this.hostname;
            object clone = new DataItem { jumps = jump, latency = ltc, hostname = host };
            return clone;
        }
    }
}
