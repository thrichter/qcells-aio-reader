using System;
using System.Text.Json.Serialization;

namespace qcells_aio_reader
{
    public class AIO_Values
    {
        public AIO_Values()
        {

        }


        public float Pv { get; set; }
        public float Load { get; set; }
        public float Inv { get; set; }
        public float Demand { get; set; }
        public float FeedIn { get; set; }
        public float Grid { get; set; }
        public float BatteryP { get; set; }
        public float BatterySoc { get; set; }
        public float Temp { get; set; }
        public float ADC { get; set; }
    }
}
