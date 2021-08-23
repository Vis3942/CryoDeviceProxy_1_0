using CryoDeviceProxy.Logs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CryoDeviceProxy.BLL
{
    /// <summary>
    /// The class <c>DeviceBLL</c> handles all communications with the backend server.
    /// </summary>
    public class DeviceBLL
    {
        private static HttpClient client = new HttpClient();
        //private ConcurrentDictionary<string, PingPacket> device_db_cache_data = new ConcurrentDictionary<string, PingPacket>(); // local cache for database; to remove this after db connection

        /// <summary>
        /// Wrapper function that initiates a separate task to post the ping packet received from device to the backend server. 
        /// </summary>
        /// <param name="pp_recv">The <c>PingPacket</c> object that contains the details of the ping packet received.</param>
        /// <param name="context_str">The context string that contains the calling task information for metrics and tracing.</param>
        /// <returns>String array obtained as a result of task execution</returns>
        public static string[] SendDevicePingData(PingPacket pp_recv, string context_str)
        {
            // send query to ROR server and ensure that serial_number eand imei_number_str exists in system
            // response byte format 
            /*
            a.Bit 7: 0 = ack, 1 = re - send
            b.Bit 6: from database id 9: 0 = none, 1 = firmware available
            c.Bit 5: from database id 12: 0 = none, 1 = new language pack available
            d.Bit4: from database id 19: 0 = no approval, 1 = unit approved for pay - per - use
            e.Bit3: from database id 20: 0 = unit is free, 1 = unit is pay - per - use
            f.Bit2: from database id 21: 0 = unit ops normal, 1 = de - commission unit
            g.Bit1: waiting for connection
            h.Bit0: reserved – default 0
            each bit   "1" or "0" for each "x;x;x;x;x;x;x;x"
            */

            // if database contains pp.SerialNumber, then return approx respone, else return -1;
            //device_db_cache_data.AddOrUpdate(pp_recv.SerialNumber, pp_recv, (k, v) => v);
            // send to db
            Task<string[]> task = Task.Run(() => PingDataAsync(pp_recv, context_str));
            task.Wait();
            return task.Result;
        }

        /// <summary>
        /// This function implements an asynchronous call to the backend server with device ping data, and returns the response
        /// received in a string array, or a null string array if error or exception. 
        /// If the device was found in the backend server, the response array returned from this function contains
        /// <list type="bullet">
        /// <item><description>Index 0: service_tag</description></item>
        /// <item><description>Index 1: respone string</description></item>
        /// </list>
        /// <para>
        /// Response string obtained from server is a semicolon separated values, "x0;x1;x2;x3;x4;x5;x6;x7" where <c>x</c>'s can take values
        /// "1" or "0". The interpretation for the device is: </para>
        /// <list type="bullet">
        /// <item><description>x0: "0" => ack for ping data, "1" => retransmit ping packet </description></item>
        /// <item><description>x1: "0" => no firmware available, "1" => firmware for device is available </description></item>
        /// <item><description>x2: "0" => no language pack available, "1" => language pack for device is available </description></item>
        /// <item><description>x3: "0" => not approved for pay-per-use, "1" => approved as a pay-per-use device </description></item>
        /// <item><description>x4: "0" => unit is free device, "1" => unit is a pay-per-use device </description></item>
        /// <item><description>x5: "0" => allowed all operations, "1" => unit is decommissioned </description></item>
        /// <item><description>x6: "0" => no user interface connection request, "1" => user interface connection pending </description></item>
        /// <item><description>x7: reserved, default is always "0"</description></item>
        /// </list>
        /// </summary>
        /// <param name="pp_recv">The <c>PingPacket</c> object containing the information about the ping data received from the device.</param>
        /// <param name="context_str">The context string that contains the calling task information for metrics and tracing.</param>
        /// <returns>String array based on the response of the server or error or exception</returns>
        private static async Task<string[]> PingDataAsync(PingPacket pp_recv, string context_str)
        {
            Uri uri = new Uri(string.Format("http://{0}{1}", "127.0.0.1:8001", "/api/devices/ping"));
            CDPLogger.Verbose(uri.ToString(), context_str);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var prj = new PingRequestJson
            {
                serial_number = pp_recv.SerialNumber,
                imei_number = pp_recv.IMEINumberString,
                ping_data = pp_recv.ReceivePacketStr // System.Text.ASCIIEncoding.ASCII.GetString(pp_recv.ReceivePacket);
            };
            string jsonstr = JsonSerializer.Serialize(prj);
            var content = new StringContent(jsonstr, Encoding.UTF8, "application/json");
            string[] ret_str_a = null;
            try
            {
                HttpResponseMessage result = await client.PostAsync(uri, content);
                if (result.IsSuccessStatusCode)
                {
                    if (result.Content != null)
                    {
                        string responseBody = await result.Content.ReadAsStringAsync();
                        CDPLogger.Verbose(responseBody, context_str);
                        if (result.Content.Headers.GetValues("Content-Type").ToArray()[0].Equals("application/json"))
                        {
                            var respjson = JsonSerializer.Deserialize<PingResponseJson>(responseBody);
                            //Console.WriteLine("finished deserializer");
                            // return service tag, response data 
                            CDPLogger.Debug(string.Format($"response succ: {result.IsSuccessStatusCode}"), context_str);
                            if (result.IsSuccessStatusCode)
                            {
                                if ((pp_recv.SerialNumber.Equals(respjson.data.serial_number)) &&
                                    (pp_recv.IMEINumberString.Equals(respjson.data.imei_number))
                                    )
                                {
                                    ret_str_a = new string[2];
                                    ret_str_a[0] = respjson.data.service_tag;
                                    ret_str_a[1] = respjson.data.response;
                                }
                            }
                        }
                        else
                        {
                            ret_str_a = new string[2];
                            ret_str_a[0] = "error";
                            ret_str_a[1] = "did not get json resp from server.";
                        }
                    }
                    else
                    {
                        throw new Exception("Received null content");
                    }
                }
                else
                {
                    throw new Exception($"Received Unsuccessful status code {result.StatusCode}");
                }
            }
            catch (Exception e)
            {
                CDPLogger.Error(string.Format("Ping: {0}", e.Message), context_str);
                if (e.InnerException != null)
                {
                    CDPLogger.Error(string.Format("Ping: {0}", e.InnerException.Message), context_str);
                }
                ret_str_a = null;
            }
            return ret_str_a;
        }


        /// <summary>
        /// Wrapper function that initiates a separate task to read specific device information from the backend server.
        /// This request is usually sent from the user interface.
        /// </summary>
        /// <param name="service_tag">service_tag of the device whose information is requested</param>
        /// <param name="email">email of the user logged in the user interface for verification</param>
        /// <param name="token">Authentication token of the user logged in the user interface</param>
        /// <param name="context_str">The context string that contains the calling task information for metrics and tracing.</param>
        /// <returns>String array obtained as a result of task execution</returns>
        public static string[] GetDeviceInfo(string service_tag, string email, string token, string context_str)
        {
            Task<string[]> task = Task.Run(() => GetDeviceDataAsync(service_tag, email, token, context_str));
            task.Wait();
            return task.Result;
        }

        /// <summary>
        /// This function implements an asynchronous call to the backend server to read the device information, and returns the response
        /// received in a string array, or a null string array if error or exception. 
        /// If the device was found in the backend server, the response array returned from this function contains
        /// <list type="bullet">
        /// <item><description>Index 0: ping data received</description></item>
        /// <item><description>Index 1: date and time when the ping data was received</description></item>
        /// </list>
        /// </summary>
        /// <param name="service_tag">service_tag of the device whose information is requested</param>
        /// <param name="email">email of the user logged in the user interface for verification</param>
        /// <param name="token">Authentication token of the user logged in the user interface</param>
        /// <param name="context_str">The context string that contains the calling task information for metrics and tracing.</param>
        /// <returns>String array based on the information received from the backend server or null if error</returns>
        private static async Task<string[]> GetDeviceDataAsync(string service_tag, string email, string token, string context_str)
        {
            Uri uri = new Uri(string.Format("http://{0}{1}", "127.0.0.1:8001", "/api/devices/get-device"));
            CDPLogger.Verbose(uri.ToString(), context_str);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(token);
            var upr = new UiProxyRequestJson();
            upr.email = email;
            upr.service_tag = service_tag;
            string jsonstr = JsonSerializer.Serialize(upr);
            HttpRequestMessage req_msg = new HttpRequestMessage(HttpMethod.Post, uri);
            //req_msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req_msg.Content = new StringContent(jsonstr, Encoding.UTF8, "application/json");

            string[] ret_str_a = null;
            try
            {
                HttpResponseMessage result = await client.SendAsync(req_msg);
                if (result.IsSuccessStatusCode)
                {
                    if (result.Content != null)
                    {
                        string responseBody = await result.Content.ReadAsStringAsync();
                        CDPLogger.Verbose(responseBody, context_str);
                        if (result.Content.Headers.GetValues("Content-Type").ToArray()[0].Equals("application/json"))
                        {
                            var respjson = JsonSerializer.Deserialize<UiProxyResponseJson>(responseBody);
                            if (result.IsSuccessStatusCode)
                            {
                                ret_str_a = new string[2];
                                ret_str_a[0] = respjson.data.ping_data;
                                ret_str_a[1] = respjson.data.updated_at;
                            }
                            //else
                            //{
                            //    ret_str_a = new string[2];
                            //    ret_str_a[0] = "error";
                            //    ret_str_a[1] = "did not recive json response";
                            //}
                        }
                    }
                    else
                    {
                        throw new Exception("Received null content");
                    }
                }
                else
                {
                    throw new Exception($"Received Unsuccessful status code {result.StatusCode}");
                }
                //var content = new StringContent(jsonstr, Encoding.UTF8, "application/json");
                //HttpResponseMessage result = await client.PostAsync(uri, content);

            }
            catch (Exception e)
            {
                CDPLogger.Error(string.Format("GetDeviceData: {0}", e.Message), context_str);
                ret_str_a = null;
            }
            return ret_str_a;
        }


        /// <summary>
        /// Wrapper function that initiates a separate task to verify a connection request from user in user interface at the backend server. 
        /// This is usually executed when the user interface requests a connection to a device. If the user is authenticated correctly
        /// the response is true, else it is false
        /// </summary>
        /// <param name="service_tag">service_tag of the device whose information is requested</param>
        /// <param name="email">email of the user logged in the user interface for verification</param>
        /// <param name="token">Authentication token of the user logged in the user interface</param>
        /// <param name="context_str">The context string that contains the calling task information for metrics and tracing.</param>
        /// <returns></returns>
        public static bool VerifyConnectRequest(string service_tag, string email, string token, string context_str)
        {
            Task<bool> task = Task.Run(() => VerifyConnectRequestAsync(service_tag, email, token, context_str));
            task.Wait();
            return task.Result;
        }

        /// <summary>
        /// This function implements an asynchronous call to the backend server to verify the connection request user logged in
        /// at the user interface, and returns the response received as a boolean.
        /// If authentication is successful, it returns true, else false
        /// </summary>
        /// <param name="service_tag">service_tag of the device whose information is requested</param>
        /// <param name="email">email of the user logged in the user interface for verification</param>
        /// <param name="token">Authentication token of the user logged in the user interface</param>
        /// <returns></returns>
        private static async Task<bool> VerifyConnectRequestAsync(string service_tag, string email, string token, string context_str)
        {
            Uri uri = new Uri(string.Format("http://{0}{1}", "127.0.0.1:8001", "/api/devices/connect-verify"));
            CDPLogger.Verbose(uri.ToString(), context_str);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(token);
            var upr = new UiProxyRequestJson
            {
                email = email,
                service_tag = service_tag
            };
            string jsonstr = JsonSerializer.Serialize(upr);
            HttpRequestMessage req_msg = new HttpRequestMessage(HttpMethod.Post, uri);
            req_msg.Content = new StringContent(jsonstr, Encoding.UTF8, "application/json");

            bool toret_bool = false;
            try
            {
                HttpResponseMessage result = await client.SendAsync(req_msg);
                if (result.IsSuccessStatusCode)
                {
                    if (result.Content != null)
                    {
                        string responseBody = await result.Content.ReadAsStringAsync();
                        CDPLogger.Verbose(responseBody, context_str);
                        if (result.Content.Headers.GetValues("Content-Type").ToArray()[0].Equals("application/json"))
                        {
                            var respjson = JsonSerializer.Deserialize<UiProxyResponseJson>(responseBody);
                            if (result.IsSuccessStatusCode)
                            {
                                if ((respjson.data.response != null) && (respjson.data.response.Equals("true")))
                                {
                                    toret_bool = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        throw new Exception("Received null content");
                    }
                }
                else
                {
                    throw new Exception($"Received Unsuccessful status code {result.StatusCode}");
                }
            }
            catch (Exception e)
            {
                CDPLogger.Error(e, "VerifyConnect", context_str);   //Conxole.WriteLine(string.Format("VerifyConnect: {0}", e.Message));
                toret_bool = false;
            }
            return toret_bool;
        }


        /// <summary>
        /// Wrapper function that initiates a separate task that update the error list from device. 
        /// </summary>
        /// <param name="service_tag">The device from which the error list was read</param>
        /// <param name="err_list">Array of error list strings</param>
        /// <param name="context_str">The context string that contains the calling task information for metrics and tracing.</param>
        /// <returns></returns>
        public static bool UpdateDeviceErrors(string service_tag, string[] err_list, string context_str)
        {
            Task<bool> task = Task.Run(() => UpdateDeviceErrorsAsync(service_tag, err_list, context_str));
            task.Wait();
            return task.Result;
        }

        /// <summary>
        /// This function implements an asynchronous call to the backend server with error list
        /// </summary>
        /// <param name="service_tag">The device from which the error list was read</param>
        /// <param name="err_list">Array of error list strings</param>
        /// <param name="context_str">The context string that contains the calling task information for metrics and tracing.</param>
        /// <returns>true if successfully updated or false otherwise</returns>
        private static async Task<bool> UpdateDeviceErrorsAsync(string service_tag, string[] err_list, string context_str)
        {
            Uri uri = new Uri(string.Format("http://{0}{1}", "127.0.0.1:8001", "/api/devices/update-device-errors"));
            CDPLogger.Verbose(uri.ToString(), context_str);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            DeviceError[] dev_err = new DeviceError[err_list.Length];
            for (int ii = 0; ii < err_list.Length; ii++)
            {
                string[] tmp_comps = err_list[ii].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (tmp_comps.Length >= 3)
                {
                    DeviceError de = new DeviceError
                    {
                        occured_at = tmp_comps[1].Trim(),
                        error_code = tmp_comps[2].Trim()
                    };
                    dev_err[ii] = de;
                }
            }
            UpdateErrorListRequestJson uelrj = new UpdateErrorListRequestJson();
            uelrj.device = service_tag;
            uelrj.error_list = dev_err;
            string jsonstr = JsonSerializer.Serialize(uelrj);

            HttpRequestMessage req_msg = new HttpRequestMessage(HttpMethod.Post, uri);
            req_msg.Content = new StringContent(jsonstr, Encoding.UTF8, "application/json");

            bool toret_bool = false;
            try
            {
                HttpResponseMessage result = await client.SendAsync(req_msg);
                if (result.IsSuccessStatusCode)
                {
                    if (result.Content != null)
                    {
                        string responseBody = await result.Content.ReadAsStringAsync();
                        CDPLogger.Verbose(responseBody, context_str);
                        if (result.Content.Headers.GetValues("Content-Type").ToArray()[0].Equals("application/json"))
                        {
                            var respjson = JsonSerializer.Deserialize<UiProxyResponseJson>(responseBody);
                            if ((respjson.data.response != null) && (respjson.data.response.Equals("true")))
                            {
                                toret_bool = true;
                            }
                        }
                    }
                    else
                    {
                        throw new Exception("Received null content");
                    }
                }
                else
                {
                    throw new Exception($"Received Unsuccessful status code {result.StatusCode}");
                }
            }
            catch (Exception e)
            {
                // Debug
                CDPLogger.Error(string.Format("UpdateErrList: {0}", e.Message), context_str);
                toret_bool = false;
            }
            return toret_bool;
        }


        /// <summary>
        /// Wrapper function that initiates a separate task to update usage statistics read from the device 
        /// </summary>
        /// <param name="service_tag"></param>
        /// <param name="usagestats_list"></param>
        /// <param name="context_str">The context string that contains the calling task information for metrics and tracing.</param>
        /// <returns>Boolean value returned by the task execution</returns>
        public static bool UpdateUsageStats(string service_tag, string[] usagestats_list, string context_str)
        {
            Task<bool> task = Task.Run(() => UpdateUsageStatsAsync(service_tag, usagestats_list, context_str));
            task.Wait();
            return task.Result;
        }

        /// <summary>
        /// This function implements an asynchronous call to the backend server with device usage statistics
        /// The database column name and corresponding data in the usage statistics payload is shown below
        /// <list type="table">
        /// <item><description>DB column name => data received from device</description></item>
        /// <item><description>cartridge_tag_id => ID:E004010802ACDB4D</description></item>
        /// <item><description>serum_name => Name:Form1</description></item>
        /// <item><description>serum_part_number => PartNumber: 893215614</description></item>
        /// <item><description>serum_lot_number => LotNumber: 1234565</description></item>
        /// <item><description>treatment_time => Treatment Time: 241</description></item>
        /// <item><description>datetime_used => Used on:10/08/20/ 03:55</description></item>
        /// <item><description>board_temp => Average Board temperature: 30.60</description></item>
        /// <item><description>min_co2_temp => Min Exit temperature: -10.60</description></item>
        /// <item><description>max_co2_temp => Max Exit temperature: 29.10</description></item>
        /// <item><description>avg_co2_temp =>  Average CO2 temperature: -2.50</description></item>
        /// <item><description>tank_tag_id => Tank ID:E004010801C764C5</description></item>
        /// <item><description>tank_time_remaining => Tank Time Remaining: 59213</description></item>
        /// </list>
        /// </summary>
        /// <param name="service_tag">service_tag of the device whose information is requested</param>
        /// <param name="usagestats_list">usage statitistics read from the device</param>
        /// <param name="context_str">The context string that contains the calling task information for metrics and tracing.</param>
        /// <returns>True if usage statistics was successfully uploaded to server, else false</returns>
        private static async Task<bool> UpdateUsageStatsAsync(string service_tag, string[] usagestats_list, string context_str)
        {

            /*
             *  twelve (12) consecutive elements belong to one usage stats
             *  db column name          ->  data as received from device
                cartridge_tag_id		-> ID:E004010802ACDB4D
                serum_name				-> Name:Form1
                serum_part_number	    -> PartNumber: 893215614
                serum_lot_number        -> LotNumber: 1234565
                treatment_time			-> Treatment Time: 241
                datetime_used			-> Used on:10/08/20/ 03:55
                board_temp  			-> Average Board temperature: 30.60
                min_co2_temp			-> Min Exit temperature: -10.60
                max_co2_temp			-> Max Exit temperature: 29.10
                avg_co2_temp			-> Average CO2 temperature: -2.50
                tank_tag_id				-> Tank ID:E004010801C764C5
                tank_time_remaining		-> Tank Time Remaining: 59213
             * 
             */
            // allow for formats with and without PartNumber
            //if (usagestats_list.Length % 11 != 0)
            //{
            //    return false;
            //}

            Uri uri = new Uri(string.Format("http://{0}{1}", "127.0.0.1:8001", "/api/devices/update-device-usagestats"));
            CDPLogger.Verbose(uri.ToString(), context_str);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            List<DeviceUsageStats> devusage_list = new List<DeviceUsageStats>();
            //int tmp_len = usagestats_list.Length / 11;
            //DeviceUsageStats[] dev_usagestats = new DeviceUsageStats[tmp_len];
            DeviceUsageStats to_add_dus = null;
            for (int kk = 0; kk < usagestats_list.Length; kk++)
            {
                string[] parsed_strs = Utils.ValuesSplitAtColon(usagestats_list[kk]);
                // looking for Cartridge:6 line to start a new usage statistics
                if (parsed_strs[0].ToLower().Equals("ID".ToLower()))
                {
                    if (to_add_dus != null)
                    {
                        devusage_list.Add(to_add_dus);
                        to_add_dus = null;
                    }
                    to_add_dus = new DeviceUsageStats
                    {
                        cartridge_tag_id = parsed_strs[1]
                    };
                }
                else
                {
                    if (to_add_dus != null)
                    {
                        if (parsed_strs[0].ToLower().Equals("Name".ToLower()))
                        {
                            to_add_dus.serum_name = parsed_strs[1];
                        }
                        else if (parsed_strs[0].ToLower().Equals("PartNumber".ToLower()))
                        {
                            to_add_dus.serum_part_number = parsed_strs[1];
                        }
                        else if (parsed_strs[0].ToLower().Equals("LotNumber".ToLower()))
                        {
                            to_add_dus.serum_lot_number = parsed_strs[1];
                        }
                        else if (parsed_strs[0].ToLower().Equals("Treatment Time".ToLower()))
                        {
                            to_add_dus.treatment_time = parsed_strs[1];
                        }
                        else if (parsed_strs[0].ToLower().Equals("Used on".ToLower()))
                        {
                            to_add_dus.datetime_used = parsed_strs[1];
                        }
                        else if (parsed_strs[0].ToLower().Equals("Average Board temperature".ToLower()))
                        {
                            to_add_dus.board_temperature = parsed_strs[1];
                        }
                        else if (parsed_strs[0].ToLower().Equals("Min Exit temperature".ToLower()))
                        {
                            to_add_dus.min_co2_temperature = parsed_strs[1];
                        }
                        else if (parsed_strs[0].ToLower().Equals("Max Exit temperature".ToLower()))
                        {
                            to_add_dus.max_co2_temperature = parsed_strs[1];
                        }
                        else if (parsed_strs[0].ToLower().Equals("Average CO2 temperature".ToLower()))
                        {
                            to_add_dus.average_co2_temperature = parsed_strs[1];
                        }
                        else if (parsed_strs[0].ToLower().Equals("Tank ID".ToLower()))
                        {
                            to_add_dus.tank_tag_id = parsed_strs[1];
                        }
                        else if (parsed_strs[0].ToLower().Equals("Tank Time Remaining".ToLower()))
                        {
                            to_add_dus.tank_time_remaining = parsed_strs[1];
                        }
                    }
                }
            }
            if (to_add_dus != null)
            {
                devusage_list.Add(to_add_dus);
            }

            UpdateUsageStatsListRequestJson uuslrj = new UpdateUsageStatsListRequestJson
            {
                device = service_tag,
                usagestats_list = devusage_list.ToArray()
            };
            string jsonstr = JsonSerializer.Serialize(uuslrj);

            HttpRequestMessage req_msg = new HttpRequestMessage(HttpMethod.Post, uri);
            req_msg.Content = new StringContent(jsonstr, Encoding.UTF8, "application/json");

            bool toret_bool = false;
            try
            {
                HttpResponseMessage result = await client.SendAsync(req_msg);
                if (result.IsSuccessStatusCode)
                {
                    if (result.Content != null)
                    {
                        string responseBody = await result.Content.ReadAsStringAsync();
                        CDPLogger.Verbose(responseBody, context_str);
                        if (result.Content.Headers.GetValues("Content-Type").ToArray()[0].Equals("application/json"))
                        {
                            var respjson = JsonSerializer.Deserialize<UiProxyResponseJson>(responseBody);
                            if ((respjson.data.response != null) && (respjson.data.response.Equals("true")))
                            {
                                toret_bool = true;
                            }
                            else
                            {
                                toret_bool = false;
                            }
                        }
                    }
                    else
                    {
                        throw new Exception("Received null content");
                    }
                }
                else
                {
                    throw new Exception($"Received Unsuccessful status code {result.StatusCode}");
                }
            }
            catch (Exception e)
            {
                // Debug
                CDPLogger.Error(string.Format("UpdateUsageStatsList: {0}", e.Message), context_str);
                toret_bool = false;
            }
            return toret_bool;
        }


        /// <summary>
        /// Wrapper function that initiates a separate task to update the Invalid Ping Data received from device. 
        /// Invalid ping data occurs if the data format is incorrect, or the device is not found in the Device table.
        /// </summary>
        /// <param name="recv_pkt_str">The string received from Device which was incorrect.</param>
        /// <param name="context_str">The context string that contains the calling task information for metrics and tracing.</param>
        /// <returns>String array that is generated in the task code.</returns>
        public static string[] SetInvalidPingData(string recv_pkt_str, string context_str)
        {
            // send to db
            Task<string[]> task = Task.Run(() => InvalidPingData(recv_pkt_str, context_str));
            task.Wait();
            return task.Result;
        }


        /// <summary>
        /// This function implements an asynchronous call to with invalid device ping data, and returns the response
        /// received in a string array, or a null string array if error or exception. 
        /// The response array returned from this function contains
        /// <list type="bullet">
        /// <item><description>Index 0: "success" or "failure"</description></item>
        /// <item><description>Index 1: respone string from server</description></item>
        /// </list>
        /// </summary>
        /// <param name="recv_pkt_str">The string received from Device which was incorrect.</param>
        /// <param name="context_str">The context string that contains the calling task information for metrics and tracing.</param>
        /// <returns>String array</returns>
        private static async Task<string[]> InvalidPingData(string recv_pkt_str, string context_str)
        {
            Uri uri = new Uri(string.Format("http://{0}{1}", "127.0.0.1:8001", "/api/devices/invalid-ping"));
            CDPLogger.Verbose(uri.ToString(), context_str);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var prj = new InvalidPingJson
            {
                invalid_ping_data = recv_pkt_str
            };
            string jsonstr = JsonSerializer.Serialize(prj);
            var content = new StringContent(jsonstr, Encoding.UTF8, "application/json");
            string[] ret_str_a = null;
            try
            {
                HttpResponseMessage result = await client.PostAsync(uri, content);
                if (result.IsSuccessStatusCode)
                {
                    if (result.Content != null)
                    {
                        string responseBody = await result.Content.ReadAsStringAsync();
                        CDPLogger.Verbose(responseBody, context_str);
                        if (result.Content.Headers.GetValues("Content-Type").ToArray()[0].Equals("application/json"))
                        {
                            var respjson = JsonSerializer.Deserialize<PingResponseJson>(responseBody);
                            //Console.WriteLine("finished deserializer");
                            // return service tag, response data 
                            CDPLogger.Debug(string.Format($"response succ: {result.IsSuccessStatusCode}"), context_str);
                            if (result.IsSuccessStatusCode)
                            {
                                ret_str_a = new string[2];
                                ret_str_a[0] = "success";
                                ret_str_a[1] = respjson.data.response;
                            }
                        }
                        else
                        {
                            ret_str_a = new string[2];
                            ret_str_a[0] = "failed";
                            ret_str_a[1] = "did not get json resp from server.";
                        }
                    }
                    else
                    {
                        throw new Exception("Received null content");
                    }
                }
                else
                {
                    throw new Exception($"Received Unsuccessful status code {result.StatusCode}");
                }
            }
            catch (Exception e)
            {
                CDPLogger.Error(string.Format("InvalidPing: {0}", e.Message), context_str);
                ret_str_a = null;
            }
            return ret_str_a;
        }
    }

}
