using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace CryoDeviceProxy
{
    public class Utils
    {

        /// <summary>
        /// This utility function checks for equality of data in two given arrays
        /// </summary>
        /// <param name="array1">first array for comparison</param>
        /// <param name="arr1_start_index">start of data in first array</param>
        /// <param name="array2">second array for comparison</param>
        /// <param name="arr2_start_index">start of data in the second array</param>
        /// <param name="comp_length">length of data to be compared in the 2 arrays</param>
        /// <returns></returns>
        public static bool BytesEqual(byte[] array1, int arr1_start_index, byte[] array2, int arr2_start_index, int comp_length)
        {
            if (((arr1_start_index + comp_length) > array1.Length) || ((arr2_start_index + comp_length) > array2.Length))
            {
                return false;
            }


            bool ret_sts = true;
            int ii;
            ii = 0;
            while ((ret_sts) && (ii < comp_length))
            {
                if (array1[arr1_start_index + ii] == array2[arr2_start_index + ii])
                {
                    ret_sts = ret_sts && true;
                }
                else
                {
                    ret_sts = ret_sts && false;
                }
                ii++;
            }
            return ret_sts;
        }

        //public static void SendToClientString(NetworkStream net_stream, string send_str)
        //{
        //    byte[] send_b = System.Text.ASCIIEncoding.ASCII.GetBytes(send_str);
        //    Utils.SendToClient(net_stream, send_b);
        //}


        /// <summary>
        /// This utility function can be used asynchronously write byte array data to the sslstream
        /// </summary>
        /// <param name="net_stream">stream to write data to</param>
        /// <param name="data_send">bytes to write to stream</param>
        public static async void SendToClient(SslStream net_stream, byte[] data_send)
        {
            if (net_stream == null)
            {
                return;
            }
            await net_stream.WriteAsync(data_send);
        }

        /// <summary>
        /// This utility function converts the text into base64encoded string
        /// </summary>
        /// <param name="plainText"></param>
        /// <returns></returns>
        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        /// <summary>
        /// This utility function is a wrapper for the split function that splits based on the colon.
        /// If there is no colon, it returns a 2 element array with the first element as the actual string
        /// and the second element as an empty string ("")
        /// </summary>
        /// <param name="to_parse_str">string to parse at colon</param>
        /// <returns>Two element string array</returns>
        public static string[] ValuesSplitAtColon(string to_parse_str)
        {
            string[] to_ret_str = new string[2];
            int index = to_parse_str.IndexOf(':');
            if (index == -1)
            {
                to_ret_str[0] = to_parse_str.Trim();
                to_ret_str[1] = "";
            }
            else
            {
                to_ret_str[0] = to_parse_str.Substring(0, index).Trim();
                to_ret_str[1] = to_parse_str[(index + 1)..].Trim();
            }
            return to_ret_str;
        }

        /// <summary>
        /// This utility function is used to read the value after the colon if it exists.
        /// </summary>
        /// <param name="to_parse_str">string to parse at the colon</param>
        /// <returns>The string after the colon if colon exists, else returns the original string</returns>
        public static string GetValueAfterColon(string to_parse_str)
        {
            if (!(to_parse_str.Contains(":") ) )
            {
                return to_parse_str;
            }

            int index = to_parse_str.IndexOf(':');
            if (index == -1)
            {
                return to_parse_str;
            }
            else
            {
                return to_parse_str[(index + 1)..].Trim();
            }

        }

    }
}
