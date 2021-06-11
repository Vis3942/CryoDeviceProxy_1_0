using CryoDeviceProxy.Logs;
using System;
using System.Collections.Generic;
using System.Text;

namespace CryoDeviceProxy
{
    public class PingPacket
    {

        #region static_funcs
        /// <summary>
        /// Based on the format "imei number;serial number; ...", this function 
        /// extracts the serial number from ping packet and returns the serial number as an integer
        /// </summary>
        /// <param name="recv_ping_str">The ping packet to parse</param>
        /// <returns>serial number as integer else -1 if error</returns>
        public int GetSerialNumber(string recv_ping_str)
        {
            /**
             * returns the serial number in the ping packet 
             * assumed format "<imei number>;<ser num>; ...." 
             *   => integer between first and second semicolon 
             */

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
            bool succ = int.TryParse(recv_ping_str.Substring(first_idx + 1, second_idx - first_idx - 1), out int pp_ser_num);
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
        private readonly byte[] _imei_number = new byte[8];
        private string _imei_number_str = string.Empty;
        private string _serial_number_str = "none";
        private string _firmware_version_str = "0";
        private ushort _new_usage_statistics = 0;
        private int _num_stored_lang_packs = 0;
        private string _ip_addr_device = "0.0.0.0";
        private readonly byte[] _recv_str_as_bytes = null;
        private bool _valid_pkt = false;
        private int _error_count = 0;
        private readonly string _recv_str;
        #endregion // vars

        #region property
        /// <summary>
        /// This property returns the length of ping packet stored in this instance
        /// </summary>
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

        /// <summary>
        /// This property returns the byte array of the ping packet stored in this instance
        /// </summary>
        public byte[] ReceivePacket
        {
            get
            {
                return _recv_str_as_bytes;
            }
        }
        public string ReceivePacketStr
        {
            get
            {
                return _recv_str;
            }
        }

        /// <summary>
        /// This property returns the IMEI number as a utf-8 byte array
        /// </summary>
        public byte[] IMEINumber
        {
            get { return _imei_number; }
        }

        /// <summary>
        /// This property returns the IMEI number as string extracted from the ping packet stored in this instance
        /// </summary>
        public string IMEINumberString
        {
            get { return _imei_number_str; }
        }

        /// <summary>
        /// This property returns the serial number as string extracted from the ping packet stored in this instance
        /// </summary>
        public string SerialNumber
        {
            get { return _serial_number_str; }
        }

        /// <summary>
        /// This property returns the firmware version string extracted from the ping packet stored in this instance
        /// </summary>
        public string FirmwareVersion
        {
            get { return _firmware_version_str; }
        }

        /// <summary>
        /// This property returns the count of usage statistics extracted from the ping packet stored in this instance
        /// </summary>
        public ushort NewUsageStatistics
        {
            get { return _new_usage_statistics; }
        }

        /// <summary>
        /// This property returns the number of language packs extracted from the ping packet stored in this instance
        /// </summary>
        public int NumStoredLanguagePacks
        {
            get { return _num_stored_lang_packs; }
        }

        /// <summary>
        /// This property to return the ip address of device extracted from ping packet stored in this instance. 
        /// Please note the latest format does not publish the ip address of device. Therefore it is not used in this version.
        /// </summary>
        public string DeviceIpAddressStr
        {
            get { return _ip_addr_device; }
        }

        /// <summary>
        /// This property indicates whether the ping packet instance is well-formed with the required number of fields.
        /// If well formed, it is true, else it is false.
        /// </summary>
        public bool IsPacketValid
        {
            get { return _valid_pkt; }
        }

        /// <summary>
        /// This property returns the number of error statistics extracted from the ping packet stored in this instance
        /// </summary>
        public int ErrorCount
        {
            get { return _error_count; }
        }
        #endregion // property

        /// <summary>
        /// This constructor can be used to initialize a new PingPacket. Required data are in the parameters.
        /// </summary>
        /// <param name="recv_data">Byte array of ping packet string as UTF-8 bytes</param>
        /// <param name="start_idx">The start index of the ping packet in the array</param>
        /// <param name="recv_len">The length of bytes to process in the array for the ping packet</param>
        public PingPacket(byte[] recv_data, int start_idx, int recv_len)
        {
            _recv_str_as_bytes = new byte[recv_len];
            System.Array.Copy(recv_data, start_idx, _recv_str_as_bytes, 0, recv_len);
            _recv_str = System.Text.ASCIIEncoding.ASCII.GetString(recv_data);
            ProcessData();
            //ProcessReceivedPacketBytes(recv_data);
        }


        /// <summary>
        /// This constructor is used to instantiate a new PingPacket based on the ping packet sent from the device. 
        /// This constructor delegates parsing and verification to the function <c>ProcessData</c>
        /// </summary>
        /// <param name="recv_str"></param>
        public PingPacket(string recv_str)
        {
            _recv_str = recv_str;
            ProcessData();
        }

        /// <summary>
        /// This function parses the ping packet string to extract the various parameters in the ping packet and assign them to 
        /// appropriate fields of the class <c>PingPacket</c>. After parsing, the function will set the validated flag, which is read
        /// by other functions to ascertain the integrity of the ping packet. 
        /// </summary>
        private void ProcessData()
        {
            /* v10 format
            Receive ping string data packets with a ‘;’ separator from a connected unit(ping interval 5 minutes) with the following format:
            a.String 0: IMEI number string[always 15 characters];
            b.String 1: unit serial number string[up to 10 characters];
            c.String 2: new usage statistics available[up to 10 characters];
            d.String 3: new error count available
            delimiter[0x0D]
            */

            /*
            v11 format
            A;B;C;D;E;F;G;H1;...Hn\r
            where 
            [0] A = IMEI 
            [1] B = serial number
            [2] C = stats counts
            [3] D = error count
            [4] E = firmware version
            [5] F = PPU grace treatments left
            [6] G = total number of language packs
            [7.. 7 + item[6] - 1]Hi  = each language pack name with version number and i = 0...G-1 
            */

            //string mod_str = System.Text.ASCIIEncoding.ASCII.GetString(recv_data);
            CDPLogger.Verbose(_recv_str.Length.ToString());
            string[] pp_arr = _recv_str.Trim().Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var tstr in pp_arr)
            {
                CDPLogger.Verbose($"{tstr.Length} => [{tstr}]");
            }
            bool sts;
            if (pp_arr.Length == 4 )
            {
                // old packet format
                sts = GetUsageCountUshort(pp_arr[2], out _new_usage_statistics);
                if (sts)
                {
                    sts = GetIntValue(pp_arr[3], out _error_count);
                }

                if (sts)
                {
                    // _new_usage_statistics and _error_count has already been populated
                    _imei_number_str = pp_arr[0];
                    _serial_number_str = pp_arr[1];
                    _firmware_version_str = "0";
                    _valid_pkt = true;
                }
                else
                {
                    _imei_number_str = string.Empty;
                    _serial_number_str = "none";
                    _new_usage_statistics = 0;
                    _firmware_version_str = "0";
                    _error_count = 0;
                    _valid_pkt = false;
                }
            }
            else
            {
                // check if it is new format
                // must have a minimum of 7 or more elements
                bool is_valid_new_format = false;
                if (pp_arr.Length >= 7)
                {
                    bool succ = GetIntValue(pp_arr[6], out int lang_pack_count);
                    if (succ)
                    {
                        if (pp_arr.Length == 7 + lang_pack_count)
                        {
                            sts = GetUsageCountUshort(pp_arr[2], out _new_usage_statistics);
                            if (sts)
                            {
                                sts = GetIntValue(pp_arr[3], out _error_count);
                            }

                            if (sts)
                            {
                                // valid packet
                                //_new_usage_statistics and _error_count has already been populated
                                _imei_number_str = pp_arr[0];
                                _serial_number_str = pp_arr[1];
                                _firmware_version_str = pp_arr[4];
                                _valid_pkt = true;
                                is_valid_new_format = true;
                            }
                        }
                    }
                }
                if (!is_valid_new_format)
                {
                    _serial_number_str = "invalid";
                    _imei_number_str = "invalid";
                    _new_usage_statistics = 0;
                    _num_stored_lang_packs = 0;
                    _ip_addr_device = "None.None.None.None";
                    _valid_pkt = false;
                }
            }
        }

        /// <summary>
        /// The <c>GetUsageCountUshort</c> function is a wrapper for ushort conversion which parses the usage count string as a ushort variable, and returns
        /// true if successfully converted else false
        /// </summary>
        /// <param name="usage_count_str">String to convert</param>
        /// <param name="usage_count">Out variable that contains the converted value on successfuly conversion</param>
        /// <returns>true if conversion successful else false</returns>
        private bool GetUsageCountUshort(string usage_count_str, out ushort usage_count)
        {
            bool ret_sts = ushort.TryParse(usage_count_str, out ushort tmp_val);
            if (ret_sts)
            {
                usage_count = tmp_val;
            }
            else
            {
                usage_count = 0;
            }
            return ret_sts;
        }

        /// <summary>
        /// The <c>GetIntValue</c> is a wrapper for converting string to integer, and returns true if conversion is successful, else false
        /// </summary>
        /// <param name="value_str">the string to convert to int</param>
        /// <param name="value">Out param that contains the converted value on successful conversion</param>
        /// <returns>True if conversion is successful else returns false</returns>
        private bool GetIntValue(string value_str, out int value)
        {
            bool ret_sts = int.TryParse(value_str, out int tmp_val);
            if (ret_sts)
            {
                value = tmp_val;
            }
            else
            {
                value = 0;
            }
            return ret_sts;
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
