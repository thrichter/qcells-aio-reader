using System;
namespace qcells_aio_reader
{
    public class MonthlyValues
    {

        public float PvMonth { get; set; }
        public float LoadMonth { get; set; }
        public float InvMonth { get; set; }
        public float DemandMonth { get; set; }
        public float FeedInMonth { get; set; }
        public string MonthIndex { get; set; }
        public string YearIndex { get; set; }
    }
}
