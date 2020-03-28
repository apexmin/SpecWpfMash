using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace SpecWpfMash
{
    public class TableViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<TableData> ResultDatas { get; }

        public TableViewModel()
        {
            ResultDatas = new ObservableCollection<TableData>
           {
               new TableData
               {
                    No = 1,
                    ItemA = "Jan",
                    ItemB = "Happy",
                    enumType = enumTypes.Plus
               },
               new TableData
               {
                    No = 2,
                    ItemA = "Feb",
                    ItemB = "Good",
                    enumType = enumTypes.Minus
               },
           };
        }

    }

    public class TableData
    {
        public bool IsChecked { get; set; }
        public int No { get; set; }
        public string ItemA { get; set; }
        public string ItemB { get; set; }
        public enumTypes enumType { get; set; }
    }
    public enum enumTypes
    {
        Plus, Minus, Zero
    }
}
