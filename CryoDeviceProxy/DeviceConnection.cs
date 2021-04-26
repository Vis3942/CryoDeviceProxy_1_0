using CryoDeviceProxy.Logs;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace CryoDeviceProxy
{
    public class DeviceConnection
    {
        #region vars
        private int _serial_num;
        private TcpClient _ui_client = null;
        private TcpClient _device_client = null;
        private object lock_device_client = new object();
        private const int DEFAULT_UI_MESSAGE_INTERVAL_SECS = 15;
        private int _ui_message_interval_secs = 15;
        byte[] end_b = System.Text.ASCIIEncoding.ASCII.GetBytes("end\n");
        private AsyncAutoResetEvent aare = new AsyncAutoResetEvent();
        private AsyncAutoResetEvent wait_conn_end = new AsyncAutoResetEvent();


        #endregion // vars
        #region property
        public int SerialNumber
        {
            get { return _serial_num; }
        }

        public TcpClient DeviceClient
        {
            get
            {
                lock(lock_device_client)
                {
                    return _device_client;
                }
            }

            set
            {
                lock(lock_device_client)
                {
                    _device_client = value;
                }
            }
        }

        public AsyncAutoResetEvent WaitTillCompleted
        {
            get
            {
                return aare;
            }
        }

        public AsyncAutoResetEvent WaitConnectionEnd
        {
            get
            {
                return wait_conn_end;
            }
        }


        #endregion //property

        public DeviceConnection(int loc_serial_num, TcpClient loc_ui_client)
        {
            _serial_num = loc_serial_num;
            _ui_client = loc_ui_client;
            _ui_message_interval_secs = DEFAULT_UI_MESSAGE_INTERVAL_SECS;
            //response_timer = new Timer();
            //response_timer.Interval = _ui_message_interval_secs;
            //response_timer.Elapsed += DoUIResponse;
            //response_timer.Enabled = true;
        }

        //private void DoUIResponse(Object source, ElapsedEventArgs e)
        //{
        //    TcpClient f_dev_client = DeviceClient;
        //    if (f_dev_client == null)
        //    {
        //        SendToUIClientString("Waiting for Connection.\n");
        //    }
        //}

        private void SendToUIClientString(NetworkStream net_stream, string send_str)
        {
            byte[] send_b = System.Text.ASCIIEncoding.ASCII.GetBytes(send_str);
            SendToUIClient(net_stream, send_b);
        }

        private async void SendToUIClient(NetworkStream net_stream, byte[] data_send)
        {
            await net_stream.WriteAsync(data_send);
        }

        public void SetDeviceClient(TcpClient loc_device_client)
        {
            DeviceClient = loc_device_client;
            //response_timer.Enabled = false; // stop sending connected messages
            
            //Task.Run(() => DeviceUIConnectionAsync());
        }

        public async void DeviceUIConnectionAsync()
        {
            //bool ui_client_error = false;
            //bool device_client_error = false;
            byte[] read_dev_b = new byte[4096];
            byte[] read_ui_b = new byte[4096];
            int dev_bytes_read;
            int ui_bytes_read;
            //byte[] write_b = new byte[4096];
            using (var ui_stream = _ui_client.GetStream())
            {
                TcpClient loc_dev_client = DeviceClient;
                while (loc_dev_client == null)
                {
                    SendToUIClientString(ui_stream, "Waiting for Connection.\n");
                    CDPLogger.Information("sent waiting for connect string.");
                    System.Threading.Thread.Sleep(_ui_message_interval_secs * 1000);
                    loc_dev_client = DeviceClient;
                }
                SendToUIClientString(ui_stream, "connected\n");
                CDPLogger.Information("Sent Connected string");
                using (var device_stream = loc_dev_client.GetStream())
                {
                    while (true)
                    {
                        // read from ui client; write to device client
                        // read from device client; write to ui client
                        // the above 2 steps can happen in any sequence, so best keep it async
                        // if error in ui client/device client, close both clients
                        //CDPLogger.Verbose("in exchange while loop");
                        var uiReadTask = ui_stream.ReadAsync(read_ui_b, 0, read_ui_b.Length);
                        var devReadTask = device_stream.ReadAsync(read_dev_b, 0, read_dev_b.Length);
                        var doneTask = await Task.WhenAny(new Task[] { devReadTask, uiReadTask }).ConfigureAwait(false);
                        if (doneTask == devReadTask)
                        {
                            if (devReadTask.IsFaulted || devReadTask.IsCanceled)
                            {
                                //device_client_error = true;
                                break;
                            }

                            dev_bytes_read = devReadTask.Result;
                            if (dev_bytes_read == 0)
                            {
                                //device_client_error = true;
                                break;
                            }
                            else
                            {
                                CDPLogger.Verbose(string.Format("sending to ui: byte len {0}", dev_bytes_read));
                                await ui_stream.WriteAsync(read_dev_b, 0, dev_bytes_read);
                            }
                            if (Utils.BytesEqual(end_b, 0, read_dev_b, 0, dev_bytes_read))
                            {
                                CDPLogger.Information("device sent end command. Exiting device ui connection.");
                                break;
                            }


                        }

                        if (doneTask == uiReadTask)
                        {
                            if ( (uiReadTask.IsFaulted) || (uiReadTask.IsCanceled) )
                            {
                                //ui_client_error = true;
                                break;
                            }
                            ui_bytes_read = uiReadTask.Result;
                            if (ui_bytes_read == 0)
                            {
                                //ui_client_error = true;
                                break;
                            }
                            else
                            {
                                CDPLogger.Verbose(string.Format("sending to device: byte len {0}", ui_bytes_read));
                                await device_stream.WriteAsync(read_ui_b, 0, ui_bytes_read);
                            }
                            if (Utils.BytesEqual(end_b, 0, read_ui_b, 0, ui_bytes_read))
                            {
                                CDPLogger.Information("ui sent end command. Exiting device ui connection.");
                                break;
                            }
                        }
                        uiReadTask = null;
                        devReadTask = null;
                        //CDPLogger.Verbose("end of exchange while loop");
                    }

                }
            }
            CloseAll();
            aare.Set();
            wait_conn_end.Set();
        }

        public void CloseAll()
        {
            if (_ui_client != null)
            {
                _ui_client.Close();
                _ui_client.Dispose();
                _ui_client = null;
            }
            TcpClient _tmpdev_client = DeviceClient;
            if (_tmpdev_client != null)
            {
                _tmpdev_client.Close();
                _tmpdev_client.Dispose();
                _tmpdev_client = null;
                DeviceClient = null;
            }
        }

        #region db_ror_funcs
        #endregion //db_ror_funcs

    }
}
