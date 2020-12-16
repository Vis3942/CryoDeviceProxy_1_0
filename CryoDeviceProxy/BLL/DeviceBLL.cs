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
    public class DeviceBLL
    {

        private ConcurrentDictionary<string, PingPacket> device_db_cache_data = new ConcurrentDictionary<string, PingPacket>(); // local cache for database; to remove this after db connection


        public string[] SendDevicePingData(PingPacket pp_recv)
        {
            // send query to ROR server and ensure that serial_number eand imei_number_str exists in system
            // TODO: code to send query to ROR server and get the response
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
            Task<string[]> task = Task.Run(() => PingDataAsync(pp_recv));
            task.Wait();
            return task.Result;
        }

        private async Task<string[]> PingDataAsync(PingPacket pp_recv)
        {
            using (var client = new HttpClient())
            {
                Uri uri = new Uri(string.Format("http://{0}{1}", "127.0.0.1:3000", "/api/devices/ping"));
                //Console.WriteLine(uri);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var prj = new PingRequestJson();
                prj.serial_number = pp_recv.SerialNumber;
                prj.imei_number = pp_recv.IMEINumberString;
                prj.ping_data = System.Text.ASCIIEncoding.ASCII.GetString(pp_recv.ReceivePacket);
                string jsonstr = JsonSerializer.Serialize(prj);
                var content = new StringContent(jsonstr, Encoding.UTF8, "application/json");
                string[] ret_str_a = null;
                try
                {
                    HttpResponseMessage result = await client.PostAsync(uri, content);
                    string responseBody = await result.Content.ReadAsStringAsync();
                    if (result.Content.Headers.GetValues("Content-Type").ToArray()[0].Equals("application/json"))
                    {
                        var respjson = JsonSerializer.Deserialize<PingResponseJson>(responseBody);
                        //Console.WriteLine("finished deserializer");
                        // return service tag, response data 
                        //Console.WriteLine("response");
                        Console.WriteLine(result.IsSuccessStatusCode);
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
                catch (Exception e)
                {
                    Console.WriteLine(string.Format("Ping: {0}", e.Message));
                    ret_str_a = null;
                }
                return ret_str_a;
            }
        }


        //public List<PingPacket> GetActiveDeviceList()
        //{
        //    List<PingPacket> to_ret_pps = new List<PingPacket>(device_db_cache_data.Values);
        //    return to_ret_pps;
        //}

        //public byte[] GetDeviceInfo(string serial_number_str)
        //{
        //    if (device_db_cache_data.ContainsKey(serial_number_str) )
        //    {
        //        PingPacket pp = device_db_cache_data[serial_number_str];
        //        return pp.ReceivePacket;
        //    }
        //    else
        //    {
        //        return null;
        //    }
        //}

        public string DeviceExistsInDB(string serial_number_str, string imei_number_str)
        {
            DeviceBLL devbll = Program.GetDeviceBLL();
            if (devbll == null)
            {
                return string.Empty;
            }

            // send query to ROR server and ensure that serial_number eand imei_number_str exists in system
            // TODO: code to send query to ROR server and get the response
            // response byte format 
            /*
                Send Ack data packet 7 strings delimited by ‘;’ back to unit consisting of one byte with the following bit fields:
                a. string0: 0 = ack, 1 = re-send
                b. string1: from database id 9: 0 = none, 1 = firmware available
                c. String2: from database id 12: 0 = none, 1 = new language pack available
                d. String3: from database id 19: 0 = no approval, 1 = unit approved for pay-per-use
                e. String4: from database id 20: 0 = unit is free, 1 = unit is pay-per-use
                f. String5: from database id 21: 0 = unit ops normal, 1 = de-commission unit
                g. String6: 1: UI waiting for connection
                h. String7: reserved
                i. String8: 0x0D
                each bit   "1" or "0" for each "x;x;x;x;x;x;x;x"
            */
            return "0;0;0;0;0;0;0;0\xd";


        }

        public string[] GetDeviceInfo(string service_tag, string email, string token)
        {
            Task<string[]> task = Task.Run(() => GetDeviceDataAsync(service_tag, email, token));
            task.Wait();
            return task.Result;
        }
        private async Task<string[]> GetDeviceDataAsync(string service_tag, string email, string token)
        {
            using (var client = new HttpClient())
            {
                Uri uri = new Uri(string.Format("http://{0}{1}", "127.0.0.1:3000", "/api/devices/get-device"));
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
                    //var content = new StringContent(jsonstr, Encoding.UTF8, "application/json");
                    //HttpResponseMessage result = await client.PostAsync(uri, content);

                    string responseBody = await result.Content.ReadAsStringAsync();
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
                catch (Exception e)
                {
                    Console.WriteLine(string.Format("GetDeviceData: {0}", e.Message));
                    ret_str_a = null;
                }
                return ret_str_a;

            }
        }


        public bool VerifyConnectRequest(string service_tag, string email, string token)
        {
            Task<bool> task = Task.Run(() => VerifyConnectRequestAsync(service_tag, email, token));
            task.Wait();
            return task.Result;
        }

        private async Task<bool> VerifyConnectRequestAsync(string service_tag, string email, string token)
        {
            using (var client = new HttpClient())
            {
                Uri uri = new Uri(string.Format("http://{0}{1}", "127.0.0.1:3000", "/api/devices/connect-verify"));
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(token);
                var upr = new UiProxyRequestJson();
                upr.email = email;
                upr.service_tag = service_tag;
                string jsonstr = JsonSerializer.Serialize(upr);
                HttpRequestMessage req_msg = new HttpRequestMessage(HttpMethod.Post, uri);
                req_msg.Content = new StringContent(jsonstr, Encoding.UTF8, "application/json");

                bool toret_bool = false;
                try
                {
                    HttpResponseMessage result = await client.SendAsync(req_msg);

                    string responseBody = await result.Content.ReadAsStringAsync();

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
                catch (Exception e)
                {
                    Console.WriteLine(string.Format("VerifyConnect: {0}", e.Message));
                    toret_bool = false;
                }
                return toret_bool;
            }
        }


        public bool UpdateDeviceErrors(string service_tag, string[] err_list)
        {
            Task<bool> task = Task.Run(() => UpdateDeviceErrorsAsync(service_tag, err_list));
            task.Wait();
            return task.Result;
        }

        private static async Task<bool> UpdateDeviceErrorsAsync(string service_tag, string[] err_list)
        {
            using (var client = new HttpClient())
            {
                Uri uri = new Uri(string.Format("http://{0}{1}", "127.0.0.1:3000", "/api/devices/update-device-errors"));
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                DeviceError[] dev_err = new DeviceError[err_list.Length];
                for (int ii = 0; ii < err_list.Length; ii++)
                {
                    string[] tmp_comps = err_list[ii].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (tmp_comps.Length >= 3)
                    {
                        DeviceError de = new DeviceError();
                        de.occured_at = tmp_comps[1].Trim();
                        de.error_code = tmp_comps[2].Trim();
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

                    string responseBody = await result.Content.ReadAsStringAsync();
                    //Console.WriteLine(responseBody);
                    if ((result.IsSuccessStatusCode) &&
                        (result.Content.Headers.GetValues("Content-Type").ToArray()[0].Equals("application/json"))
                        )
                    {
                        var respjson = JsonSerializer.Deserialize<UiProxyResponseJson>(responseBody);
                        if ((respjson.data.response != null) && (respjson.data.response.Equals("true")))
                        {
                            toret_bool = true;
                        }
                    }

                }
                catch (Exception e)
                {
                    // Debug
                    Console.WriteLine(string.Format("UpdateErrList: {0}", e.Message));
                    toret_bool = false;
                }
                return toret_bool;
            }
        }


        // UpdateUsageStats
        public bool UpdateUsageStats(string service_tag, string[] usagestats_list)
        {
            Task<bool> task = Task.Run(() => UpdateUsageStatsAsync(service_tag, usagestats_list));
            task.Wait();
            return task.Result;
        }


        private static async Task<bool> UpdateUsageStatsAsync(string service_tag, string[] usagestats_list)
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

            using (var client = new HttpClient())
            {
                Uri uri = new Uri(string.Format("http://{0}{1}", "127.0.0.1:3000", "/api/devices/update-device-usagestats"));
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
                        to_add_dus = new DeviceUsageStats();
                        to_add_dus.cartridge_tag_id = parsed_strs[1];
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

                UpdateUsageStatsListRequestJson uuslrj = new UpdateUsageStatsListRequestJson();
                uuslrj.device = service_tag;
                uuslrj.usagestats_list = devusage_list.ToArray();
                string jsonstr = JsonSerializer.Serialize(uuslrj);

                HttpRequestMessage req_msg = new HttpRequestMessage(HttpMethod.Post, uri);
                req_msg.Content = new StringContent(jsonstr, Encoding.UTF8, "application/json");

                bool toret_bool = false;
                try
                {
                    HttpResponseMessage result = await client.SendAsync(req_msg);

                    string responseBody = await result.Content.ReadAsStringAsync();
                    Console.WriteLine(responseBody);
                    if ((result.IsSuccessStatusCode) &&
                        (result.Content.Headers.GetValues("Content-Type").ToArray()[0].Equals("application/json"))
                        )
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
                catch (Exception e)
                {
                    // Debug
                    Console.WriteLine(string.Format("UpdateUsageStatsList: {0}", e.Message));
                    toret_bool = false;
                }
                return toret_bool;
            }
        }

        private static async Task<bool> UpdateUsageStatsAsyncOld(string service_tag, string[] usagestats_list)
        {
            /*
             *  eleven (11) consecutive elements belong to one usage stats
             *  db column name          ->  data as received from device
                cartridge_tag_id		-> Cartridge: 8
                serum_lot_number		-> ID:E004010802ACDB4D
                serum_name				-> Name:Form1
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
            if (usagestats_list.Length % 12 != 0)
            {
                return false;
            }
            using (var client = new HttpClient())
            {
                Uri uri = new Uri(string.Format("http://{0}{1}", "127.0.0.1:3000", "/api/devices/update-device-usagestats"));
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                int tmp_len = usagestats_list.Length / 11;
                DeviceUsageStats[] dev_usagestats = new DeviceUsageStats[tmp_len];
                for (int kk = 0; kk < tmp_len; kk++)
                {
                    DeviceUsageStats dus = new DeviceUsageStats();
                    dus.cartridge_tag_id = Utils.GetValueAfterColon(usagestats_list[11 * kk]);
                    dus.serum_part_number = Utils.GetValueAfterColon(usagestats_list[11 * kk + 1]);
                    dus.serum_name = Utils.GetValueAfterColon(usagestats_list[11 * kk + 2]);
                    dus.treatment_time = Utils.GetValueAfterColon(usagestats_list[11 * kk + 3]);
                    dus.datetime_used = Utils.GetValueAfterColon(usagestats_list[11 * kk + 4]);
                    dus.board_temperature = Utils.GetValueAfterColon(usagestats_list[11 * kk + 5]);
                    dus.min_co2_temperature = Utils.GetValueAfterColon(usagestats_list[11 * kk + 6]);
                    dus.max_co2_temperature = Utils.GetValueAfterColon(usagestats_list[11 * kk + 7]);
                    dus.average_co2_temperature = Utils.GetValueAfterColon(usagestats_list[11 * kk + 8]);
                    dus.tank_tag_id = Utils.GetValueAfterColon(usagestats_list[11 * kk + 9]);
                    dus.tank_time_remaining = Utils.GetValueAfterColon(usagestats_list[11 * kk + 10]);
                    dev_usagestats[kk] = dus;
                }
                UpdateUsageStatsListRequestJson uuslrj = new UpdateUsageStatsListRequestJson();
                uuslrj.device = service_tag;
                uuslrj.usagestats_list = dev_usagestats;
                string jsonstr = JsonSerializer.Serialize(uuslrj);

                HttpRequestMessage req_msg = new HttpRequestMessage(HttpMethod.Post, uri);
                req_msg.Content = new StringContent(jsonstr, Encoding.UTF8, "application/json");

                bool toret_bool = false;
                try
                {
                    HttpResponseMessage result = await client.SendAsync(req_msg);

                    string responseBody = await result.Content.ReadAsStringAsync();
                    Console.WriteLine(responseBody);
                    if ((result.IsSuccessStatusCode) &&
                        (result.Content.Headers.GetValues("Content-Type").ToArray()[0].Equals("application/json"))
                        )
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
                catch (Exception e)
                {
                    // Debug
                    Console.WriteLine(string.Format("UpdateUsageStatsList: {0}", e.Message));
                    toret_bool = false;
                }
                return toret_bool;
            }
        }


    }

}
