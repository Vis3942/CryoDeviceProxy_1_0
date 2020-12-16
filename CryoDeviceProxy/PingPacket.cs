using System;
using System.Collections.Generic;
using System.Text;

namespace CryoDeviceProxy
{
    public class PingPacket
    {

        #region static_funcs
        /**
         * returns the serial number in the ping packet 
         * assumed format "<imei number>;<ser num>; ...." 
         *   => integer between first and second semicolon 
         */
        public int GetSerialNumber(string recv_ping_str)
        {

            // serial number is "<imei number>;<ser num>; ...." 
            // => integer between first and second serial number
            int first_idx = recv_ping_str.IndexOf(';');
            if (first_idx == -1)
            {
                return -1;
            }
            int second_idx = recv_ping_str.IndexOf(';', first_idx + 1);
            if ( (second_idx == -1) || (second_idx == (first_idx + 1) ) )
            {
                return -1;
            }
            int pp_ser_num;
            bool succ = int.TryParse(recv_ping_str.Substring(first_idx + 1, second_idx - first_idx - 1), out pp_ser_num);
            if (succ)
            {
                return pp_ser_num;
            }
            else
            {
                return -1;
            }
        }
        #endregion // static_funcs

        #region vars
        private byte[] _imei_number = new byte[8];
        private string _imei_number_str = string.Empty;
        private string _serial_number_str = "none";
        private byte _firmware_version = 0;
        private ushort _new_usage_statistics = 0;
        private ushort _num_stored_lang_packs = 0;
        private string _ip_addr_device = "0.0.0.0";
        private byte[] _recv_str_as_bytes = null;
        private bool _valid_pkt = false;
        private int _error_count = 0;
        #endregion // vars

        #region property

        public int ReceivePacketLength
        {
            get
            {
                if (_recv_str_as_bytes == null)
                {
                    return 0;
                }
                else
                {
                    return _recv_str_as_bytes.Length;
                }
            }
        }

        public byte[] ReceivePacket
        {
            get
            {
                return _recv_str_as_bytes;
            }
        }

        public byte[] IMEINumber
        {
            get { return _imei_number; }
        }

        public string IMEINumberString
        {
            get { return _imei_number_str; }
        }

        public string SerialNumber
        {
            get { return _serial_number_str; }
        }

        public byte FirmwareVersion
        {
            get { return _firmware_version; }
        }

        public ushort NewUsageStatistics
        {
            get { return _new_usage_statistics; }
        }

        public ushort NumStoredLanguagePacks
        {
            get { return _num_stored_lang_packs; }
        }

        public string DeviceIpAddressStr
        {
            get { return _ip_addr_device; }
        }

        public bool IsPacketValid
        {
            get { return _valid_pkt; }
        }

        public int ErrorCount
        {
            get { return _error_count; }
        }
        #endregion // property

        public PingPacket(byte[] recv_data, int recv_len)
        {
            _recv_str_as_bytes = new byte[recv_len];
            System.Array.Copy(recv_data, 0, _recv_str_as_bytes, 0, recv_len);
            ProcessDataAsString(recv_data);
            //ProcessReceivedPacketBytes(recv_data);
        }

        private void ProcessDataAsString(byte[] recv_data)
        {
            string mod_str = System.Text.ASCIIEncoding.ASCII.GetString(recv_data);
            string[] pp_arr = mod_str.Split(';');
            if (pp_arr.Length >= 4 )
            {
                _imei_number_str = pp_arr[0];
                _serial_number_str = pp_arr[1];
                _new_usage_statistics = ushort.Parse(pp_arr[2]);
                _firmware_version = 0;
                _error_count = int.Parse(pp_arr[3]);
                _valid_pkt = true;
            }
            else
            {
                _serial_number_str = "invalid";
                _imei_number_str = "invalid";
                _new_usage_statistics = 0;
                _num_stored_lang_packs = 0;
                _ip_addr_device = "None.None.None.None";
                _valid_pkt = false;
            }
        }

        //private void ProcessReceivedPacketBytes(byte[] data)
        //{
            //System.Array.Copy(data, 0, _imei_number, 0, 8);
            //_serial_number = BitConverter.ToUInt16(data, 8);
            //_firmware_version = data[10];
            //_new_usage_statistics = BitConverter.ToUInt16(data, 11);
            //_num_stored_lang_packs = BitConverter.ToUInt16(data, 13);
            //_ip_addr_device = string.Format("%d.%d.%d.%d", data[15], data[16], data[17], data[18]);
            //GenerateIMEINumberString();
        //}

        //private void GenerateIMEINumberString()
        //{
        //    StringBuilder sb = new StringBuilder();
        //    int count = 0;
        //    while (count < 16)
        //    {
        //        foreach (byte bb in _imei_number)
        //        {
        //            int digit1 = ((bb >> 4) & 0x000000FF);
        //            int digit2 = (bb & 0x000000FF);
        //            if (count < 16)
        //            {
        //                sb.Append(digit1.ToString());
        //                count++;
        //            }
        //            if (count < 16)
        //            {
        //                sb.Append(digit2.ToString());
        //                count++;
        //            }
        //        }
        //    }
        //}

    }
}
