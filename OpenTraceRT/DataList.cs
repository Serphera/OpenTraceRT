using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Collections;

namespace OpenTraceRT {
    class DataList : List<List<DataItem>>, INotifyPropertyChanged {

        public void OnPropertyChanged(object sender, PropertyChangedEventArgs e) {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) {
                handler(sender, e);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
