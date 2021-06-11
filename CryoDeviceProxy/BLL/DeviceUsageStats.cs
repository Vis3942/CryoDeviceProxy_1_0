using System;
using System.Collections.Generic;
using System.Text;

namespace CryoDeviceProxy.BLL
{
    /// <summary>
    /// The class <c>DeviceUsageStats</c> encapsulates the usage statistics information received from device
    /// </summary>
    public class DeviceUsageStats
    {

        public DeviceUsageStats()
        {
            cartridge_tag_id = "";
            serum_part_number = "";
            serum_name = "";
            treatment_time = "";
            datetime_used = "";
            board_temperature = "";
            min_co2_temperature = "";
            max_co2_temperature = "";
            average_co2_temperature = "";
            tank_tag_id = "";
            tank_time_remaining = "";
        }
        public string cartridge_tag_id { get; set; }
        public string serum_name { get; set; }
        public string serum_part_number { get; set; }
        public string serum_lot_number { get; set; }
        public string treatment_time { get; set; }
        public string datetime_used { get; set; }
        public string board_temperature { get; set; }
        public string min_co2_temperature { get; set; }
        public string max_co2_temperature { get; set; }
        public string average_co2_temperature { get; set; }
        public string tank_tag_id { get; set; }
        public string tank_time_remaining { get; set; }
    }
}
