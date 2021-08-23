using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using CryoDeviceProxy.Logs;
using System.Linq;
using CryoDeviceProxy.BLL;

namespace CryoDeviceProxy
{

    public enum DeviceStates
    {
        UNKNOWN,
        NORMAL,
        UI_CONNECTED,
        ERROR_DOWNLOAD,
        USAGESTATS_DOWNLOAD
    }
    public class CryoServer
    {
        #region vars
        private string _ipv4_addr = IPAddress.Any.ToString();
        TcpListener deviceServer = null;
        TcpListener uiServer = null;
        // Thread _server_thread = null;
        private readonly object lock_list = new object();
        //private readonly List<string> devicesPinged = new List<string>();
        //private AutoResetEvent device_ui_connection_completed = new AutoResetEvent(false);
        private int _ui_wait_message_interval_secs = 15;
        private int _ui_connect_wait_max_count = 9;
        private readonly ConcurrentDictionary<string, TcpClient> uiClientDict = new ConcurrentDictionary<string, TcpClient>();
        private readonly ConcurrentDictionary<string, TcpClient> devClientDict = new ConcurrentDictionary<string, TcpClient>();
        private readonly ConcurrentDictionary<string, BufferBlock<byte[]>> uiBufBlock = new ConcurrentDictionary<string, BufferBlock<byte[]>>();
        private readonly ConcurrentDictionary<string, BufferBlock<byte[]>> devBufBlock = new ConcurrentDictionary<string, BufferBlock<byte[]>>();
        private readonly ConcurrentDictionary<string, SslStream> uiStreamDict = new ConcurrentDictionary<string, SslStream>();
        private readonly ConcurrentDictionary<string, SslStream> devStreamDict = new ConcurrentDictionary<string, SslStream>();
        private readonly byte[] end_b = System.Text.ASCIIEncoding.ASCII.GetBytes("end");
        private readonly byte[] bol_b = System.Text.ASCIIEncoding.ASCII.GetBytes("bol\xd");
        private readonly byte[] eol_b = System.Text.ASCIIEncoding.ASCII.GetBytes("eol\xd");
        private readonly byte[] disconnected_b = System.Text.ASCIIEncoding.ASCII.GetBytes("disconnected\xd");
        private const string PARAM_FILE_NAME = "cryodeviceproxy_params.txt";
        private int _ui_connection_port = 0; //9090 + 99;
        private int _device_connection_port = 0; //9090;
        private X509Certificate serverCertificate = null;
        private string _certificate_name = "";
        //private int _dev_read_async_timeout_secs = 5;
        private readonly List<SslStream> available_sslstreams = new List<SslStream>();

        #endregion // vars

        #region property
        public string IpAddressStr
        {
            get { return _ipv4_addr; }
            set { _ipv4_addr = value; }
        }
        public int DeviceConnectionPort
        {
            get { return _device_connection_port; }
            set { _device_connection_port = value; }
        }

        public int UIConnectionPort
        {
            get { return _ui_connection_port; }
            set { _ui_connection_port = value; }
        }

        public int UIMessageWaitInterval
        {
            get
            {
                return _ui_wait_message_interval_secs;
            }
            set
            {
                _ui_wait_message_interval_secs = value;
            }
        }

        public int UIMessageMaxWaitTimeSecs
        {
            get
            {
                return _ui_wait_message_interval_secs * _ui_connect_wait_max_count;
            }

            set
            {
                int set_val = value / _ui_wait_message_interval_secs;
                if (set_val > 8)
                {
                    _ui_connect_wait_max_count = set_val;
                }
            }
        }

        public bool IsProduction { get; set; }
        public bool ReadError { get; set; }
        public bool ClearError { get; set; }
        public bool ReadUsage { get; set; }
        public bool ClearUsageStats { get; set; }

        #endregion // property

        public CryoServer()
        {
            // default constructor
            this.IpAddressStr = IPAddress.Any.ToString();
            LoadConfig();
        }

        public CryoServer(string ipaddr)
        {
            this.IpAddressStr = ipaddr;
            LoadConfig();
        }
        /// <summary>
        /// Loads the application configuration from file cryodevproxy_params.txt
        /// The parameters file contains the following parameters
        /// <list type="bullet">
        /// <item>
        /// <description>Line 1: User Interface socket connection port.</description>
        /// </item>
        /// <item>
        /// <description>Line 2: Device Interface socket connection port</description>
        /// </item>
        /// <item>
        /// <description>Line 3: Certificate name for server without the ".pfx" extension. 
        /// App expects the file to be in the same directory as the application executable. 
        /// If the app does not find the file, the app logs an error and exits.</description>
        /// </item>
        /// </list>        
        /// </summary>
        public void LoadConfig()
        {
            int ii = 0;
            bool sts;
            int tmp_port;
            // any errors in file, it will use default values of 
            // 9090 + 99 for ui connection and 9090 for device connection
            _ui_connection_port = 9090 + 99;
            _device_connection_port = 9090;
            if (File.Exists(PARAM_FILE_NAME))
            {
                foreach (string line in File.ReadLines(PARAM_FILE_NAME))
                {
                    if (!line.Trim().StartsWith("#"))
                    {
                        if (ii == 0)
                        {
                            sts = int.TryParse(line.Trim(), out tmp_port);
                            if (sts)
                            {
                                _ui_connection_port = tmp_port;
                            }
                        }
                        else if (ii == 1)
                        {
                            sts = int.TryParse(line.Trim(), out tmp_port);
                            if (sts)
                            {
                                _device_connection_port = tmp_port;
                            }
                        }
                        else if (ii == 2)
                        {
                            _certificate_name = line.Trim() + ".pfx";
                            break;
                        }
                        ii++;
                    }
                }

                // load certificate if file found
                if (File.Exists(_certificate_name))
                {
                    LoadCertificate(_certificate_name);
                }
                else
                {
                    CDPLogger.Error(string.Format("Looking for server certificate =>[{0}]", _certificate_name));
                    CDPLogger.Error("Server certificate not found, exiting application.");
                    System.Environment.Exit(2);
                }
            }
        }

        /// <summary>
        /// Loads the X509 Certificate for TLS communications 
        /// </summary>
        /// <param name="cert_name">Name of certificate file (extension: .pfx) </param>
        private void LoadCertificate(string cert_name)
        {
            try
            {
                serverCertificate = X509Certificate.CreateFromCertFile(cert_name);
            }
            catch (Exception excp)
            {
                CDPLogger.Error(string.Format("Error loading certificate => {0}", cert_name));
                CDPLogger.Error(excp.Message);
                CDPLogger.Error("Exiting application.");
                System.Environment.Exit(1);
            }
        }

        /// <summary>
        /// This function starts the following 2 servers:
        /// <list type="bullet">
        /// <item>
        /// <description>Device Connection Listener: Server listening for connections from devices.</description>
        /// </item>
        /// <item>
        /// <description>User Interface Connection Listener: Server listening for connections from User Interface.</description>
        /// </item>
        /// </list>        
        /// </summary>
        public void StartAllServers()
        {
            CDPLogger.Information("Starting Cryo Socket Server");
            StartDeviceServerAsync();
            StartUIServerAsync();
        }

        /// <summary>
        /// This function starts the user interface server listening for connections from user interface as a separate task.
        /// </summary>
        public void StartUIServerAsync()
        {
            uiServer = new TcpListener(IPAddress.Parse(_ipv4_addr), UIConnectionPort);
            uiServer.Start();
            CDPLogger.Information("Started UI server");
            try
            {
                var task = Task.Run(() => AcceptUIClientsAsync(uiServer).ConfigureAwait(false));
                if (task.IsFaulted)
                {
                    task.Wait();
                }
            }
            finally
            {
            }
            //while (true)
            //{
            //    try
            //    {
            //        var uiTCPClient = await uiServer.AcceptTcpClientAsync();
            //        Task t = ClientTestProtocol(uiTCPClient);
            //        await t;
            //        //var cw = new ClientUnit(client, true);
            //        //Task.Run(() => cw.ProtocolAsync());
            //    }
            //    catch (Exception ex)
            //    {
            //        Console.WriteLine(ex.Message);
            //    }
            //}
        }

        /// <summary>
        /// This function starts the device server listening for connections from devices as a separate task.
        /// </summary>
        public void StartDeviceServerAsync()
        {
            deviceServer = new TcpListener(IPAddress.Parse(_ipv4_addr), DeviceConnectionPort);
            deviceServer.Start();
            CDPLogger.Information("Started Device server");
            try
            {
                var task = Task.Run(() => AcceptDeviceClientsAsync(deviceServer).ConfigureAwait(false));
                if (task.IsFaulted)
                {
                    task.Wait();
                }
            }
            finally
            {
            }

            //while (true)
            //{
            //    try
            //    {
            //        var devTCPClient = await deviceServer.AcceptTcpClientAsync();
            //        Task t = ClientTestProtocol(devTCPClient);
            //        await t;
            //        //var cw = new ClientUnit(client, true);
            //        //Task.Run(() => cw.ProtocolAsync());
            //    }
            //    catch (Exception ex)
            //    {
            //        Console.WriteLine(ex.Message);
            //    }
            //}
        }

        /// <summary>
        /// This function is the entry point of User Interface connection
        /// that is, any connection initiated by the user interface code will trigger an accept connections.
        /// This function then launches a separate task to handle the protocol between the socket server and the user interface.
        /// </summary>
        /// <param name="uiListener">the server object listening for connections from user interface</param>
        /// <returns></returns>
        async Task AcceptUIClientsAsync(TcpListener uiListener)
        {
            while (true)
            {
                try
                {
                    var client = await uiListener.AcceptTcpClientAsync().ConfigureAwait(false);
                    var task = Task.Run(() => UIProtocolAsync(client)).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    CDPLogger.Error(e, "UIAccept and Protocol Async");
                    break;
                }
            }
        }

        /// <summary>
        /// This function normally runs in its own task, and handles all protocols (that is commands and responses) to a single user interface 
        /// Firstly the tls based communications is initiated with the user interface. 
        /// If this tls negotiation fails, the connection to the user interface is closed.
        /// All connections to this socket server require an authentication token served by the backend server.
        /// Supported commands with user interface are:
        /// <list type="bullet">
        /// <item>
        /// <description>getinfo\n[service tag]\n[email]\n[token]
        /// <para>The socket server queries the backend server and after authenticating the token
        /// returns [ping data];[utc time it was received]0x0d</para></description>
        /// </item>
        /// <item>
        /// <description>connect\n[service tag]\n[email]\n[token]0x0d
        /// <para>The socket server queries the backend server and after authenticating the token
        /// waits for device to ping data and establish a connection. This function on receiving the connect command
        /// waits for period of 15*9 seconds for a ping packet from device. If the device has not sent the 
        /// ping packet, the function will end the connection to the user interface.</para>
        /// <para>If the device pings within the timeout, the function initializes the connected (Y1) command to the device, and 
        /// setups up BufferedBlocks that enable reading any data received from user interface and sends it to the device (through a buffer block),
        /// and conversely any data from device (through a buffer block) is sent to the user interface. If the user interface sends the "end" data 
        /// or closes the connection, the above bufferblocks are closed and the connection to the user interface ends. This could also be triggered by an 
        /// end command from the device (received through a BufferBlock) </para></description>
        /// </item>
        /// <item>
        /// <description> Advanced Feature
        /// <para>In case of error in the ping data were the packet received is mal-formed the device will never get a response, and remote debuggging
        /// will not be possible. To support this special case, advacned connection options are supported. All these functions behave exactly as the connect 
        /// command above but have different set of data in the command payload. The are
        /// <list>
        /// <item><description>advconnectserial\n[serial number]\n[email]\n[token]0x0d</description></item>
        /// <item><description>advconnectimei\n[imei]\n[email]\n[token]0x0d</description></item>
        /// <item><description>advconnectid\n[id in InvalidPingPacket table]\n[email]\n[token]0x0d</description></item>
        /// </list>
        /// </para>
        /// </description>
        /// </item>
        /// </list>        
        /// </summary>
        /// <param name="uiClient">Object representing connection to the User Interface</param>
        /// <returns></returns>
        async Task UIProtocolAsync(TcpClient uiClient)
        {
            var ip = uiClient.Client.RemoteEndPoint.ToString();
            string contextstr = "ui-" + ip.Trim().Replace(".", "-").Replace(":", "-");
            CDPLogger.Information($"UI client ({ip}) connected", contextstr);
            var buf_dev = new byte[4096];
            var buf_ui = new byte[4096];
            //var close_client = false;


            var stream = uiClient.GetStream();
            //using (uiClient)
            //{
            //    using (var stream = uiClient.GetStream())
            //    {

            SslStream uiSslStream = new SslStream(
                stream, false);
            try
            {
                uiSslStream.AuthenticateAsServer(serverCertificate, clientCertificateRequired: false, checkCertificateRevocation: true);
                available_sslstreams.Add(uiSslStream);
                while (true)
                {
                    CDPLogger.Verbose("start of ui protocol while true loop", contextstr);
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(3 * 60)); // 3 minutes
                    var amountReadTask = uiSslStream.ReadAsync(buf_ui, 0, buf_ui.Length);

                    var completedTask = await Task.WhenAny(timeoutTask, amountReadTask)
                                                    .ConfigureAwait(false);
                    if (completedTask == timeoutTask)
                    {
                        break;
                    }

                    if (amountReadTask.IsFaulted || amountReadTask.IsCanceled)
                    {
                        break;
                    }

                    var amountRead = amountReadTask.Result;
                    if (amountRead == 0) { break; }

                    // ui has just connected; so process received data
                    // format of bytes received
                    /*
                        Connect\n<servicetag as string\n
                        other commands will be piped after the server sends a 
                        response of Connected to ui.
                        FORMAT always <Command name>\n<parameters for command name separated by \n>
                    */
                    //Console.WriteLine(amountRead);
                    //Console.WriteLine(System.Text.ASCIIEncoding.ASCII.GetString(buf_ui, 0, amountRead));
                    if (amountRead > 0)
                    {
                        /* 
                         * getinfo\n<service tag>\n<email>\n<token>
                         */
                        string recv_str = System.Text.ASCIIEncoding.ASCII.GetString(buf_ui, 0, amountRead);
                        string[] cmds_data = recv_str.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        cmds_data[0] = cmds_data[0].Trim().ToLower();
                        if ((cmds_data[0].Equals("getinfo")) &&
                             (cmds_data.Length >= 4)
                           )
                        {
                            // returned string array contains
                            //  index 0 => recent ping data; index 1 => utc time it was received
                            string[] response_data = DeviceBLL.GetDeviceInfo(cmds_data[1].Trim(),
                                cmds_data[2].Trim(), cmds_data[3].Trim(), contextstr); // service tag, email, token 
                            if ((response_data == null) && (response_data.Length < 2))
                            {
                                byte[] list_b = System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("Device {0} not avaialable\xd", cmds_data[1]));
                                await uiSslStream.WriteAsync(list_b, 0, list_b.Length).ConfigureAwait(false);
                            }
                            else
                            {
                                StringBuilder sb = new StringBuilder();
                                sb.Append(response_data[0].Replace("\r", ""));
                                sb.Append(";");
                                sb.Append(response_data[1].Replace("\r", ""));
                                sb.Append('\xd');
                                byte[] tosend_b = System.Text.ASCIIEncoding.ASCII.GetBytes(sb.ToString());
                                await uiSslStream.WriteAsync(tosend_b, 0, tosend_b.Length).ConfigureAwait(false);
                            }
                        }
                        else if (  ( (cmds_data[0].Equals("connect")) ||
                                     (cmds_data[0].Equals("advconnectserial")) ||
                                     (cmds_data[0].Equals("advconnectimei")) ||
                                     (cmds_data[0].Equals("advconnectid"))
                                    ) &&
                                  (cmds_data.Length >= 4)
                                )
                        {
                            //ushort device_serial_number = ushort.Parse(cmds_data[1]);
                            string service_tag = string.Empty;
                            switch (cmds_data[0])
                            {
                                case "connect":
                                    service_tag = cmds_data[1].Trim();
                                    break;
                                case "advconnectserial":
                                    service_tag = "bdser" + cmds_data[1].Trim(); // bdser => back door "ser"ial 
                                    break;
                                case "advconnectimei":
                                    service_tag = "bdimei" + cmds_data[1].Trim(); // bdimei => back door imei 
                                    break;
                                case "advconnectid":
                                    service_tag = "bdid" + cmds_data[1].Trim(); // bdid => back door id 
                                    break;
                            }
                            //string service_tag = cmds_data[1].Trim();
                            if ( (cmds_data[0].Equals("connect") ) && (!DeviceBLL.VerifyConnectRequest(service_tag, cmds_data[2].Trim(), cmds_data[3].Trim(), contextstr))) // service_tag, email, token
                            {
                                byte[] no_dev_b = System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("Connect request for {0} failed Authorization\xd", service_tag));
                                await uiSslStream.WriteAsync(no_dev_b, 0, no_dev_b.Length).ConfigureAwait(false);
                            }
                            else if (uiClientDict.ContainsKey(service_tag))
                            {
                                byte[] no_dev_b = System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("Device {0} is already connected\xd", service_tag));
                                await uiSslStream.WriteAsync(no_dev_b, 0, no_dev_b.Length).ConfigureAwait(false);
                            }
                            else
                            {
                                uiClientDict.TryAdd(service_tag, uiClient);
                                BufferBlock<byte[]> tmpUiBlk = new BufferBlock<byte[]>(
                                    new DataflowBlockOptions() { BoundedCapacity = 1 });
                                uiBufBlock.TryAdd(service_tag, tmpUiBlk);
                                //var uiStream = uiClient.GetStream();
                                bool is_device_connected = devClientDict.ContainsKey(service_tag);
                                int conn_wait_count = 0;
                                while ((!is_device_connected) && (conn_wait_count < _ui_connect_wait_max_count))
                                {
                                    SendToClientString(uiSslStream, "Waiting for Connection.\xd");
                                    CDPLogger.Information("sent waiting for connect string to UI", contextstr);
                                    System.Threading.Thread.Sleep(_ui_wait_message_interval_secs * 1000);
                                    is_device_connected = devClientDict.ContainsKey(service_tag);
                                    conn_wait_count++;
                                }
                                if (is_device_connected)
                                {
                                    SendToClientString(uiSslStream, "connected\xd");
                                    CDPLogger.Information("UI: sent 'connected'.", contextstr);

                                    devBufBlock.TryGetValue(service_tag, out BufferBlock<byte[]> tmpDevBlk);
                                    devClientDict.TryGetValue(service_tag, out TcpClient devClient);
                                    devStreamDict.TryGetValue(service_tag, out SslStream devSslStream_uip);
                                    SendToClientString(devSslStream_uip, "Y1\xd");
                                    CDPLogger.Information("Device: sent 'connected'(Y1)", contextstr);
                                    // read from ui client; write to device client
                                    // read from device client; write to ui client
                                    // the above 2 steps can happen in any sequence, so best keep it async
                                    // if error in ui client/device client, close both clients
                                    Task tdevTask = null;
                                    Task tuiTask = null;
                                    CancellationTokenSource cts = new CancellationTokenSource();
                                    tuiTask = Task.Run(() =>
                                    {
                                        while (true)
                                        {
                                            try
                                            {
                                                // read from uiclient, write to tmpDevBlk
                                                var uiReadTask = uiSslStream.ReadAsync(buf_ui, 0, buf_ui.Length, cts.Token);
                                                if (uiReadTask.IsFaulted || uiReadTask.IsCanceled)
                                                {
                                                    //device_client_error = true;
                                                    tmpDevBlk.SendAsync(end_b);
                                                    tmpUiBlk.Complete();
                                                    break;
                                                }
                                                var ui_bytes_read = uiReadTask.Result;
                                                if (ui_bytes_read <= 0)
                                                {
                                                    tmpDevBlk.SendAsync(end_b);
                                                    tmpUiBlk.Complete();
                                                    break;
                                                }
                                                else
                                                {
                                                    byte[] tmp_ui_b = new byte[ui_bytes_read];
                                                    System.Array.Copy(buf_ui, 0, tmp_ui_b, 0, ui_bytes_read);
                                                    CDPLogger.Information(string.Format("Recv Len from UI {0}", ui_bytes_read), contextstr);
                                                    CDPLogger.Verbose(string.Format("Data From UI {0}",
                                                        System.Text.ASCIIEncoding.ASCII.GetString(tmp_ui_b)), contextstr);
                                                    tmpDevBlk.SendAsync(tmp_ui_b);
                                                    if (Utils.BytesEqual(tmp_ui_b, 0, end_b, 0, end_b.Length))
                                                    {
                                                        uiSslStream.WriteAsync(disconnected_b).ConfigureAwait(false);
                                                        //tmpDevBlk.Complete();
                                                        tmpUiBlk.Complete();
                                                        //cts.Cancel();
                                                        break;
                                                    }
                                                }
                                            }
                                            catch (Exception e)
                                            {
                                                CDPLogger.Error(e, "UI Pipe to Device", contextstr);
                                                tmpDevBlk.SendAsync(end_b);
                                                tmpUiBlk.Complete();
                                                break;
                                            }
                                        }
                                        //Console.WriteLine("UI: end of ui stream to dev blk");
                                    }, cts.Token);

                                    tdevTask = Task.Run(() =>
                                    {
                                        while (true)
                                        {
                                            // read from tmpuiBlk, write to devClient
                                            try
                                            {
                                                var task1 = tmpUiBlk.ReceiveAsync(cts.Token);
                                                if ((task1.IsCanceled) || (task1.IsFaulted))
                                                {
                                                    break;
                                                }
                                                byte[] tmp_dev_b = task1.Result;
                                                if (tmp_dev_b != null)
                                                {
                                                    CDPLogger.Information($"Recv Len From Device {tmp_dev_b.Length}", contextstr);
                                                    CDPLogger.Verbose($"Data from Dev {System.Text.ASCIIEncoding.ASCII.GetString(tmp_dev_b)}", contextstr);
                                                    if (Utils.BytesEqual(tmp_dev_b, 0, end_b, 0, end_b.Length))
                                                    {
                                                        uiSslStream.WriteAsync(disconnected_b).ConfigureAwait(false);
                                                        tmpDevBlk.Complete();
                                                        cts.Cancel();
                                                    }
                                                    else
                                                    {
                                                        uiSslStream.WriteAsync(tmp_dev_b).ConfigureAwait(false);
                                                    }
                                                }
                                            }
                                            catch (Exception)
                                            {
                                                break;
                                            }
                                        }
                                        //Console.WriteLine("UI: end of ui blk ui stream");
                                    }, cts.Token);
                                    var finishtask = Task.WhenAny(tdevTask, tuiTask);
                                    await finishtask;
                                    cts.Cancel();
                                    if (finishtask == tdevTask)
                                    {
                                        CDPLogger.Verbose("finished reading buf block from dev", contextstr);
                                        await tuiTask;
                                    }
                                    else
                                    {
                                        CDPLogger.Verbose("finished reading uiStream from UI", contextstr);
                                        await tdevTask;
                                    }
                                }
                                else
                                {
                                    SendToClientString(uiSslStream, "disconnected timeout\xd");
                                    CDPLogger.Information("sent disocnnect string timeout waiting for connection.", contextstr);
                                }
                            }
                            CDPLogger.Information("UI: ui device connect completed event", contextstr);
                            uiClientDict.TryRemove(service_tag, out TcpClient tmp_client);
                            uiBufBlock.TryRemove(service_tag, out BufferBlock<byte[]> tmp_blk);
                            //close_client = true;
                            //break;
                        }
                        else
                        {
                            //byte[] tosend_b = System.Text.ASCIIEncoding.ASCII.GetBytes("Invalid Command Received\n");
                            byte[] tosend_b = System.Text.ASCIIEncoding.ASCII.GetBytes("not connected\xd");
                            await uiSslStream.WriteAsync(tosend_b).ConfigureAwait(false);
                            //close_client = true;
                            break;
                        }
                    }
                }


            }
            catch (AuthenticationException ae)
            {
                CDPLogger.Error($"Exception: {ae.Message}", contextstr);
                if (ae.InnerException != null)
                {
                    CDPLogger.Error($"Inner exception: {ae.InnerException.Message}", contextstr);
                }
                CDPLogger.Error("UIProtocolAsync: Authentication failed - closing the connection.", contextstr);
                //sslStream.Close();
                //client.Close();
                //return;
            }
            catch (Exception e)
            {
                CDPLogger.Error(e, "UIProtocolAsync", contextstr);
            }
            finally
            {
                available_sslstreams.Remove(uiSslStream);
                uiSslStream.Close();
                CDPLogger.Information("Closing UI Client", contextstr);
                uiClient.Close();
                uiClient.Client.Close();
                uiClient.Dispose();
                uiClient = null;
            }
        }

        /// <summary>
        /// This function is the entry point of device connection
        /// that is, any connection initiated by the device will trigger an accept connections.
        /// This function then launches a separate task to handle the protocol between the socket server and the device.
        /// </summary>
        /// <param name="deviceListener">The server object that listens for connections from device</param>
        /// <returns></returns>
        async Task AcceptDeviceClientsAsync(TcpListener deviceListener)

        {
            while (true)
            {
                try
                {
                    var client = await deviceListener.AcceptTcpClientAsync().ConfigureAwait(false);
                    //ip = client.Client.RemoteEndPoint.ToString();
                    var task = Task.Run(() => DeviceProtocolAsync(client)).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    CDPLogger.Error(e, "Device accept async");
                    break;
                }
            }
        }

        /// <summary>
        /// This function performs a single read operation from an sslStream (first parameter), and copies the data to the buffer (second parameter).
        /// It allows the calling function to set a timeout, and the function will wait for data within that timeout.
        /// </summary>
        /// <param name="readStream">SSlstream to read from</param>
        /// <param name="readBuf">buffer to write the bytes read to</param>
        /// <param name="withTimeOut">true: apply timeout, false: wait till ssltream read call returns</param>
        /// <param name="timeout_secs">The timeout to wait for data if the "withTimeOut" parameter is true</param>
        /// <returns>0 if error or timeout, else the number of byes read from sslstream before the timeout expired</returns>
        private async Task<int> ReadFromStream(SslStream readStream, byte[] readBuf, bool withTimeOut, int timeout_secs = 3 * 60)
        {
            /*
             *  return 0 if error or fault or timeout, otherwise actual values read
             *  
             */
            if (withTimeOut)
            {
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(AppOptions.GetAppOptions.DeviceReadStreamTimeoutSecs)); // 3 minutes
                var amountReadTask = readStream.ReadAsync(readBuf, 0, readBuf.Length);

                var completedTask = await Task.WhenAny(timeoutTask, amountReadTask)
                                                .ConfigureAwait(false);
                if ((completedTask != timeoutTask) && (!amountReadTask.IsFaulted && !amountReadTask.IsCanceled))
                {
                    var amountRead = amountReadTask.Result;
                    return amountRead;
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                var amountRead = await readStream.ReadAsync(readBuf, 0, readBuf.Length);
                return amountRead;
            }
        }

        /// <summary>
        /// This function handles the protocol to download errors stored in the device.
        /// Once the data is uploaded successfully to the backend server, the error data clear command is sent to the device
        /// <para>
        /// The error data download is initiated with 
        /// Error data download command. The function will read data between 'boe' and 'eoe' as the error data.
        /// </para>
        /// </summary>
        /// <param name="deviceClient">Client object representing connection to the device</param>
        /// <param name="devSslStream">Secure tls connection for data exchange</param>
        /// <param name="servicetag_str">Identification of the device to store the error data related to this device.</param>
        /// <param name="contextstr">context for logging in multiple device tasks</param>
        /// <returns>Task</returns>
        private async Task DoErrorDownload(TcpClient deviceClient, SslStream devSslStream, string servicetag_str, string contextstr)
        {
            BufferBlock<string> devbufblk = new BufferBlock<string>(
                new DataflowBlockOptions { BoundedCapacity = 1 });
            Task tdevTask = null;
            Task tgetTask = null;
            CancellationTokenSource cts = new CancellationTokenSource();
            byte[] buf_conn = new byte[4096];
            StringBuilder sb_recv = new StringBuilder();

            Thread.Sleep(10000);
            byte[] tmpend_b3 = System.Text.ASCIIEncoding.ASCII.GetBytes("EG\xd");
            CDPLogger.Information("Sent EG command: Error data download", contextstr);
            CDPLogger.Verbose(BitConverter.ToString(tmpend_b3).Replace("-", " "), contextstr);
            await devSslStream.WriteAsync(tmpend_b3).ConfigureAwait(false);
            devSslStream.Flush();
            Thread.Sleep(5000);

            tdevTask = Task.Run(async () => {
                while (true)
                {
                    try
                    {
                        int dev_bytes_read = await devSslStream.ReadAsync(buf_conn, 0, buf_conn.Length, cts.Token);
                        if (dev_bytes_read <= 0)
                        {
                            //Console.WriteLine(string.Format("devRead bytes{0} ", dev_bytes_read));
                            //break;
                        }
                        else
                        {
                            string tmpstr = System.Text.ASCIIEncoding.ASCII.GetString(buf_conn, 0, dev_bytes_read);
                            //Console.WriteLine(string.Format("recv from dev: to process {0}", tmpstr));
                            await devbufblk.SendAsync(tmpstr);
                        }
                    }
                    catch (Exception excp)
                    {
                        CDPLogger.Error(excp, "Downloading Error Stats", contextstr);
                        devbufblk.Complete();
                        break;
                    }
                }
            }, cts.Token);

            tgetTask = Task.Run(async () => {
                while (true)
                {
                    // read from devbufBlk add to buffer
                    try
                    {
                        string recv_str = await devbufblk.ReceiveAsync(cts.Token);
                        if (recv_str != null)
                        {
                            //debug
                            //Console.WriteLine(string.Format("recv from devbufblock: {0}", recv_str));
                            sb_recv.Append(recv_str);
                            if (recv_str.Contains("eoe"))
                            {
                                devbufblk.Complete();
                                break;
                            }
                        }
                        else
                        {
                            CDPLogger.Error("recvasysn devbuf block timedout 5 seconds", contextstr);
                            break;
                        }
                    }
                    catch (Exception ee)
                    {
                        CDPLogger.Error(ee, "Reading bufblock error stat download", contextstr);
                        break;
                    }
                }
            }, cts.Token);
            var finishtask = await Task.WhenAny(tdevTask, tgetTask);
            //finishtask.Wait();
            cts.Cancel();
            if (finishtask == tdevTask)
            {
                //Console.WriteLine("tDevTask completed");
                await tgetTask;
            }
            else
            {
                //Console.WriteLine("tuiTask completed");
                await tdevTask;
            }
            CDPLogger.Information("DEVICE: completed error list download event", contextstr);
            string[] tmparr = sb_recv.ToString().Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            int boe_index = Array.FindIndex(tmparr, element => element.Trim().StartsWith("boe", StringComparison.Ordinal));
            int eoe_index = Array.FindIndex(tmparr, element => element.Trim().StartsWith("eoe", StringComparison.Ordinal));
            CDPLogger.Verbose(string.Format("boe at {0}, eoe at {1}", boe_index, eoe_index), contextstr);
            //List<string> err_list = new List<string>();
            //for (int ii = boe_index + 1; ii < eoe_index; ii++)
            //{
            //    err_list.Add(tmparr[ii]);
            //}
            //Console.WriteLine(string.Format("List[{0}], first => {1}, last => {2}", 
            //    err_list.Count, err_list[0], err_list[err_list.Count-1]));
            if ( (boe_index >= 0) && (eoe_index > 0) && (eoe_index > boe_index) )
            {
                string[] err_arr = new string[eoe_index - (boe_index + 1)];
                Array.Copy(tmparr, boe_index + 1, err_arr, 0, err_arr.Length);
                bool sts = DeviceBLL.UpdateDeviceErrors(servicetag_str, err_arr, contextstr);
                CDPLogger.Information(string.Format("sts from UpdateDevice Error call {0}", sts), contextstr);
                if ((sts) && (ClearError))
                {
                    Thread.Sleep(5000);
                    byte[] tmpend_b4 = System.Text.ASCIIEncoding.ASCII.GetBytes("EC\xd");
                    CDPLogger.Information("Sent EC command: Error data clear", contextstr);
                    CDPLogger.Verbose(BitConverter.ToString(tmpend_b4).Replace("-", " "), contextstr);
                    await devSslStream.WriteAsync(tmpend_b4).ConfigureAwait(false);
                    devSslStream.Flush();
                    sts = await ReadAckFromDeviceWithTimeout(devSslStream, 3);

                }
            }
        }

        /// <summary>
        /// This function handles the protocol to download usage statistics from the device.
        /// Once the data is uploaded successfully to the backend server, the Usage Stat clear command is sent to the device
        /// <para>
        /// The usage statistics data download is initiated with 
        /// Usage Stats download command. The function will read data between 'bos' and 'eos' as the usage statistics data.
        /// </para>
        /// </summary>
        /// <param name="deviceClient">Client object representing connection to the device</param>
        /// <param name="devSslStream">Secure tls connection for data exchange</param>
        /// <param name="servicetag_str">Identification of the device to store the usage statistics data related to this device</param>
        /// <param name="contextstr">context for logging in multiple device tasks</param>
        /// <returns>Task</returns>
        private async Task DoUsageStatsDownload(TcpClient deviceClient, SslStream devSslStream, string servicetag_str, string contextstr)
        {
            BufferBlock<string> devbufblk = new BufferBlock<string>(
                new DataflowBlockOptions { BoundedCapacity = 1 });
            Task tdevTask = null;
            Task tgetTask = null;
            CancellationTokenSource cts = new CancellationTokenSource();
            byte[] buf_conn = new byte[4096];
            StringBuilder sb_recv = new StringBuilder();

            Thread.Sleep(10000);
            byte[] tmpend_b3 = System.Text.ASCIIEncoding.ASCII.GetBytes("SA\xd");
            CDPLogger.Information("Sent SA command: Usage Stats download", contextstr);
            CDPLogger.Verbose(BitConverter.ToString(tmpend_b3).Replace("-", " "), contextstr);
            await devSslStream.WriteAsync(tmpend_b3).ConfigureAwait(false);
            devSslStream.Flush();
            Thread.Sleep(5000);

            tdevTask = Task.Run(async () => {
                while (true)
                {
                    try
                    {
                        int dev_bytes_read = await devSslStream.ReadAsync(buf_conn, 0, buf_conn.Length, cts.Token);
                        if (dev_bytes_read <= 0)
                        {
                            //Console.WriteLine(string.Format("devRead bytes{0} ", dev_bytes_read));
                            //break;
                        }
                        else
                        {
                            string tmpstr = System.Text.ASCIIEncoding.ASCII.GetString(buf_conn, 0, dev_bytes_read);
                            //Console.WriteLine(string.Format("recv from dev: to process {0}", tmpstr));
                            await devbufblk.SendAsync(tmpstr);
                        }
                    }
                    catch (Exception)
                    {
                        devbufblk.Complete();
                        break;
                    }
                }
            }, cts.Token);

            tgetTask = Task.Run(async () => {
                while (true)
                {
                    // read from devbufBlk add to buffer
                    try
                    {
                        string recv_str = await devbufblk.ReceiveAsync(cts.Token);
                        if (recv_str != null)
                        {
                            //debug
                            CDPLogger.Verbose(string.Format("recv from device: {0}", recv_str), contextstr);
                            sb_recv.Append(recv_str);
                            if (recv_str.Contains("eos"))
                            {
                                devbufblk.Complete();
                                break;
                            }
                        }
                        else
                        {
                            CDPLogger.Error("getusagestats: recvasync devbuf block timedout 5 seconds", contextstr);
                            break;
                        }
                    }
                    catch (Exception excp)
                    {
                        CDPLogger.Error(excp, "Reading buf block UsageStat download", contextstr);
                        break;
                    }
                }
            }, cts.Token);
            var finishtask = await Task.WhenAny(tdevTask, tgetTask);
            //finishtask.Wait();
            cts.Cancel();
            if (finishtask == tdevTask)
            {
                //Console.WriteLine("tDevTask completed");
                await tgetTask;
            }
            else
            {
                //Console.WriteLine("tuiTask completed");
                await tdevTask;
            }
            CDPLogger.Information("DEVICE: completed usage stats download event", contextstr);
            string[] tmparr = sb_recv.ToString().Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            int bos_index = Array.FindIndex(tmparr, element => element.Trim().StartsWith("bos", StringComparison.Ordinal));
            int eos_index = Array.FindIndex(tmparr, element => element.Trim().StartsWith("eos", StringComparison.Ordinal));
            CDPLogger.Verbose(string.Format("bos at {0}, eos at {1}", bos_index, eos_index), contextstr);
            if ( (bos_index >= 0) && (eos_index > 0)  && (eos_index > bos_index) )
            {
                string[] usagestats_arr = new string[eos_index - (bos_index + 1)];
                Array.Copy(tmparr, bos_index + 1, usagestats_arr, 0, usagestats_arr.Length);
                bool sts = DeviceBLL.UpdateUsageStats(servicetag_str, usagestats_arr, contextstr);
                CDPLogger.Information(string.Format("sts from Update Usage Statistics call {0}", sts), contextstr);
                if ((sts) && (ClearUsageStats))
                {
                    // command for clear stats ????
                    Thread.Sleep(5000);
                    byte[] tmpend_b4 = System.Text.ASCIIEncoding.ASCII.GetBytes("SC\xd");
                    CDPLogger.Information("Sent SC command: Usage Stat clear", contextstr);
                    CDPLogger.Verbose(BitConverter.ToString(tmpend_b4).Replace("-", " "), contextstr);
                    await devSslStream.WriteAsync(tmpend_b4).ConfigureAwait(false);
                    devSslStream.Flush();
                    sts = await ReadAckFromDeviceWithTimeout(devSslStream, 3);

                }
            }
        }

        /// <summary>
        /// This function reads an ACK from device with timeout. 
        /// It is mainly used in the cases of clear usage statistics or error data, which do not provide a response,
        /// but will generally respond with an ACK. Therefore, this application will knows exacly the when the command is done
        /// on the device before closing the connection to the device. 
        /// </summary>
        /// <param name="dev_stream">The TLS connection to read the ack from</param>
        /// <param name="timeout_secs">Timeout in seconds the function needs to wait for receiving an ACK</param>
        private async Task<bool> ReadAckFromDeviceWithTimeout(SslStream dev_stream, int timeout_secs)
        {
            byte[] buf_recv = new byte[5];
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeout_secs)); // 3 minutes
            var amountReadTask = dev_stream.ReadAsync(buf_recv, 0, buf_recv.Length);
            var completedTask = await Task.WhenAny(timeoutTask, amountReadTask)
                                            .ConfigureAwait(false);
            if (completedTask == timeoutTask)
            {
                CDPLogger.Error("timed out waiting for acknowledgement from Device.");
                return false;
            }
            else
            {
                CDPLogger.Verbose(string.Format("Received [{0}] for acknowledgmeent",
                    System.Text.ASCIIEncoding.ASCII.GetString(buf_recv, 0, amountReadTask.Result)));
                return true;
            }

        }

        /// <summary>
        /// This function is responsible for performing the glue code between the user interface and device.
        /// This is executed as part of the device protocol. It sets up BufferedBlocks to read from the device 
        /// and send that data to the user interface (through a buffer block), and another BufferedBlock to receive
        /// data from the user interface and then send it to the device. 
        /// </summary>
        /// <param name="deviceClient">Client object representing connection to the device</param>
        /// <param name="devSslStream">Secure tls connection for data exchange<</param>
        /// <param name="servicetag_str">Identification to establish the glue logic between user interface connection and device connection.</param>
        /// <param name="contextstr">context for logging in multiple device tasks</param>
        /// <returns></returns>
        private async Task DoUIConnection(TcpClient deviceClient, SslStream devSslStream, string servicetag_str, string contextstr)
        {
            BufferBlock<byte[]> devbufblk = new BufferBlock<byte[]>(
                new DataflowBlockOptions { BoundedCapacity = 1 });
            devBufBlock.TryAdd(servicetag_str, devbufblk);
            CDPLogger.Debug(string.Format("dev sslstream is auth {0} adding to devStreamDict", devSslStream.IsAuthenticated), contextstr);
            devStreamDict.TryAdd(servicetag_str, devSslStream);
            uiClientDict.TryGetValue(servicetag_str, out TcpClient uiClient);
            uiBufBlock.TryGetValue(servicetag_str, out BufferBlock<byte[]> uibufblk);

            //NetworkStream uiSslStream = null;
            //uiSslStream = uiClient.GetStream();
            uiStreamDict.TryGetValue(servicetag_str, out SslStream uiSslStream);
            devClientDict.TryAdd(servicetag_str, deviceClient);
            // read from dev client; write to ui buf block client
            // read from dev buf block; write to dev client
            // the above 2 steps can happen in any sequence, so best keep it async
            // if error in ui client/device client, close both clients
            Task tdevTask = null;
            Task tuiTask = null;
            CancellationTokenSource cts = new CancellationTokenSource();
            byte[] buf_conn = new byte[4096];

            tdevTask = Task.Run(async () => {
                while (true)
                {
                    try
                    {
                        // read dev clinet, write to tmpUIBlk
                        var devReadTask = devSslStream.ReadAsync(buf_conn, 0, buf_conn.Length, cts.Token);
                        if (devReadTask.IsFaulted || devReadTask.IsCanceled)
                        {
                            CDPLogger.Error("devReadTask faulted", contextstr);
                            //device_client_error = true;
                            //await uibufblk.SendAsync(disconnected_b).ConfigureAwait(false);
                            await uibufblk.SendAsync(end_b).ConfigureAwait(false);
                            devbufblk.Complete();
                            break;
                        }
                        int dev_bytes_read = devReadTask.Result;
                        //int dev_bytes_read = await devSslStream.ReadAsync(buf_conn, 0, buf_conn.Length, cts.Token);
                        if (dev_bytes_read <= 0)
                        {
                            //Console.WriteLine(string.Format("devRead bytes{0} ", dev_bytes_read));
                            //break;
                            CDPLogger.Error("devReadTask read zero bytes", contextstr);
                            //await uibufblk.SendAsync(disconnected_b).ConfigureAwait(false);
                            await uibufblk.SendAsync(end_b).ConfigureAwait(false);
                            devbufblk.Complete();
                            break;
                        }
                        else
                        {
                            byte[] tmp_ui_b = new byte[dev_bytes_read];
                            System.Array.Copy(buf_conn, 0, tmp_ui_b, 0, dev_bytes_read);
                            CDPLogger.Verbose(string.Format("recv from dev: send to ui {0}",
                                System.Text.ASCIIEncoding.ASCII.GetString(tmp_ui_b)), contextstr);
                            await uibufblk.SendAsync(tmp_ui_b);
                            if (Utils.BytesEqual(tmp_ui_b, 0, end_b, 0, end_b.Length))
                            {
                                devbufblk.Complete();
                                break;
                            }
                        }
                    }
                    catch (Exception excp)
                    {
                        CDPLogger.Error(excp, "DevReadTask", contextstr);
                        CDPLogger.Information("Closing UIConnection", contextstr);
                        byte[] to_send_end = System.Text.ASCIIEncoding.ASCII.GetBytes("end\xd");
                        await uibufblk.SendAsync(to_send_end);
                        devbufblk.Complete();
                        break;
                    }
                }
                //Console.WriteLine("end of device to ui");
            }, cts.Token);

            tuiTask = Task.Run(async () => {
                while (true)
                {
                    // read from devbufBlk, write to devClient
                    try
                    {
                        var task1 = devbufblk.ReceiveAsync(cts.Token);

                        if ((task1.IsCanceled) || (task1.IsFaulted))
                        {
                            break;
                        }
                        byte[] tmp_dev_b = task1.Result;
                        //byte[] tmp_dev_b = await devbufblk.ReceiveAsync(cts.Token);
                        if (tmp_dev_b != null)
                        {
                            CDPLogger.Verbose($"recv from ui; sent to device {System.Text.ASCIIEncoding.ASCII.GetString(tmp_dev_b)}", contextstr);
                            if (Utils.BytesEqual(tmp_dev_b, 0, end_b, 0, end_b.Length))
                            {
                                byte[] tmpend_b = System.Text.ASCIIEncoding.ASCII.GetBytes("Y0\xd");
                                CDPLogger.Information("sent disconnected (Y0) to device", contextstr);
                                await devSslStream.WriteAsync(tmpend_b).ConfigureAwait(false);
                            }
                            else
                            {
                                await devSslStream.WriteAsync(tmp_dev_b).ConfigureAwait(false);
                            }
                            if (Utils.BytesEqual(tmp_dev_b, 0, end_b, 0, end_b.Length))
                            {
                                uibufblk.Complete();
                                cts.Cancel();
                            }
                        }
                    }
                    catch (Exception ee)
                    {
                        CDPLogger.Error(ee, "Device buf block read: Ui Connection", contextstr);
                        break;
                    }
                }
                //Console.WriteLine("end of device to ui");
            }, cts.Token);
            var finishtask = await Task.WhenAny(tdevTask, tuiTask);
            //finishtask.Wait();
            cts.Cancel();
            if (finishtask == tdevTask)
            {
                //Console.WriteLine("tDevTask completed");
                await tuiTask;
            }
            else
            {
                //Console.WriteLine("tuiTask completed");
                await tdevTask;
            }
            CDPLogger.Information("DEVICE: device ui connect completed event", contextstr);
            devClientDict.TryRemove(servicetag_str, out TcpClient tmpDevClient);
            tmpDevClient.Close();
            tmpDevClient.Client.Close();
            devBufBlock.TryRemove(servicetag_str, out BufferBlock<byte[]> tmpBlk);
        }


        /// <summary>
        /// This function normally runs in its own task, and handles all protocols (that is commands and responses) to a single device 
        /// Firstly the tls based communications is initiated with the device. 
        /// If this tls negotiation fails, the connection to the device is closed.
        /// The device periodically sends a ping packet. Two versions of the ping packet are supported in this function
        /// <para>
        /// Version 1.0 ping packet format is "A;B;C;D;\r" where
        /// <list type="bullet">
        /// <item><description>A: IMEI number</description></item>
        /// <item><description>B: Serial number</description></item>
        /// <item><description>C: Usage Statistics Count</description></item>
        /// <item><description>D: Error log count</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// Version 1.1 ping packet format is "A;B;C;D;E;F;G;H1;...Hn\r" where
        /// <list type="bullet">
        /// <item><description>A: IMEI number</description></item>
        /// <item><description>B: Serial number</description></item>
        /// <item><description>C: Usage Statistics Count</description></item>
        /// <item><description>D: Error log count</description></item>
        /// <item><description>E: Firmware Version</description></item>
        /// <item><description>F: PPU Grace treatments left</description></item>
        /// <item><description>G: Number of language packs in device</description></item>
        /// <item><description>Hi: Each language pack name with version number and values of i are 0...G-1</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// The basic functionality implemented in this function on receiving a ping packet from device is:
        /// <list type="number">
        /// <item><description>Establish a TLS connection: if fails or exception, close connection to device</description></item>
        /// <item><description>Read data from stream: if error or zero bytes read, close connection to device</description></item>
        /// <item><description>Parse the read data into packets in the above format using Regex, remove duplicates and process each packet in a loop.</description></item>
        /// <item><description>Create instance of PingPacket with packet string received. If packet is not valid, write to InvalidPingData table, and close connection to device</description></item>
        /// <item><description>Packet Valid Actions:
        /// <para>
        /// <c>devbll.SendDevicePingData</c> function is used to send the ping packet to the backend server. If updated successfully, 
        /// the function will send a ping response appropriate for the following actions: "Ui Connection", "Error Data Download", "Usage Statistics Downlaod",
        /// or a ping response. The ping response packet contains information from the backend server and the connection status. 
        /// Then this function will check to see if a connection to device is requested from user interface, if yes, establish a channel with the user 
        /// interface connection object using Buffered blocks and device connection object. This setup for the device
        /// is done in the function <c>DoUIConnection</c>, and the corresponding setup for the User Interface happens in the function 
        /// <c>UIProtocolAsync</c>. Once the channel exits, connection to device and user interface are closed.
        /// If a connection is not pending, the function checks to see if there are any error data available in the device. If yes, all the 
        /// error logs from the device are downloaded and written to the backend server using the function <c>DoErrorDownload</c>. Once the error 
        /// logs have been downloaded, the connection to device is closed. 
        /// If there are no error data available in device, this function will check to see if there are any usage statistics in the device. 
        /// If usage statistics are available, the function will download the usage statistics from device and write it to the backend server 
        /// using the function <c>DoUsageStatsDownload</c>. Once data is downloaded, the connection to the device is closed. 
        /// If there is no pending usage statistics in the device, ping response is sent to the device and the connection is closed. 
        /// </para></description></item>
        /// <item><description>Packet Invalid Actions:
        /// <para>
        /// </para></description></item>
        /// </list></para>
        /// </summary>
        /// <param name="deviceClient"></param>
        /// <returns></returns>
        async Task DeviceProtocolAsync(TcpClient deviceClient)
        {
            var ip = deviceClient.Client.RemoteEndPoint.ToString();
            string contextstr = "dev-" + ip.Trim().Replace(".", "-").Replace(":", "-");
            CDPLogger.Information($"Device ({ip}) connected", contextstr);
            var buf_dev = new byte[4096];
            //var rem_recv_bytes = new byte[4096];
            //var rem_recv_count = 0;
            bool pkt_invalid = false;
            var devStream = deviceClient.GetStream();
            SslStream sslStream = new SslStream(
                devStream, false);
            try
            {
                var authTask = sslStream.AuthenticateAsServerAsync(serverCertificate, clientCertificateRequired: false, checkCertificateRevocation: true);
                var authSucc = authTask.Wait(AppOptions.GetAppOptions.DeviceSSLAuthTimeoutSecs*1000);
                if (!authSucc)
                {
                    if (authTask.Exception != null)
                    {
                        throw authTask.Exception;
                    }
                    else
                    {
                        throw new Exception("Device Authenticate as server task did not complete in 5 secs.");
                    }
                }
                //sslStream.AuthenticateAsServer(serverCertificate, clientCertificateRequired: false, checkCertificateRevocation: true);
                available_sslstreams.Add(sslStream);
                DeviceStates devState = DeviceStates.UNKNOWN; // states: "uiconnected", "getusagestats"
                // generate packet ending in 0x0d
                //StringBuilder recv_sb = new StringBuilder();
                StringBuilder remaining_sb = new StringBuilder();
                List<string> recv_pkt_strList = new List<string>();
                List<byte[]> recv_buf = new List<byte[]>();
                //recv_pkt_strList.Clear();
                //recv_buf.Clear();
                var amountRead = await ReadFromStream(sslStream, buf_dev, true);
                var strFromDev = System.Text.ASCIIEncoding.ASCII.GetString(buf_dev, 0, amountRead);
                CDPLogger.Verbose(string.Format("read [{0}]: {1}", amountRead, strFromDev), contextstr);
                if (amountRead > 0)
                {
                    recv_pkt_strList.Clear();
                    ProcessRecvByteRegex(strFromDev, recv_pkt_strList, remaining_sb);
                    recv_pkt_strList = recv_pkt_strList 
                        .GroupBy(i => i.Substring(0, 15))
                        .Select(g => g.Last())
                        .ToList();

                    if (recv_pkt_strList.Count > 0)
                    {
                        // Process recv_pkt_str_list
                        foreach (string recv_pkt_str in recv_pkt_strList)
                        {
                            pkt_invalid = false;
                            CDPLogger.Debug(string.Format("[dbg]: recv [{1}]= {0}", recv_pkt_str, recv_pkt_str.Length), contextstr);
                            // receive ping packet; device sent ping packet
                            /*
                                //Receive ping string data packets with a ‘;’ separator from a connected unit(ping interval 5 minutes) with the following format:
                                //a.String 0: IMEI number string[always 15 characters];
                                //b.String 1: unit serial number string[up to 10 characters];
                                //c.String 2: new usage statistics available[up to 10 characters];
                                //d.String 3: new error count available
                                //d.String 4: delimiter[0x0D]
                            */
                            //var pp = new PingPacket(buf_dev, 0, amountRead);
                            var pp = new PingPacket(recv_pkt_str);
                            devState = DeviceStates.NORMAL;
                            if (pp.IsPacketValid)
                            {
                                CDPLogger.Information(string.Format("serial num recv {0}, imei num {1}", pp.SerialNumber, pp.IMEINumberString), contextstr);
                                string[] response_datastrarr = DeviceBLL.SendDevicePingData(pp, contextstr);
                                if (response_datastrarr != null)
                                {
                                    string service_tag = response_datastrarr[0].Trim();
                                    StringBuilder tmp_sb = new StringBuilder(response_datastrarr[1]);
                                    if (uiClientDict.ContainsKey(service_tag))
                                    {
                                        tmp_sb[12] = '1';
                                        devState = DeviceStates.UI_CONNECTED;
                                    }
                                    else if ( ( pp.ErrorCount > 0 ) && ReadError )
                                    {
                                        tmp_sb[12] = '1';
                                        devState = DeviceStates.ERROR_DOWNLOAD;
                                    }
                                    else if ( (pp.NewUsageStatistics > 0) && ReadUsage )
                                    {
                                        tmp_sb[12] = '1';
                                        devState = DeviceStates.USAGESTATS_DOWNLOAD;
                                    }
                                    tmp_sb.Append('\xd');
                                    response_datastrarr[1] = tmp_sb.ToString();
                                    // send response
                                    CDPLogger.Information(string.Format(" sent to device: <{0}>", response_datastrarr[1]), contextstr);
                                    SendToClientString(sslStream, response_datastrarr[1]);
                                    CDPLogger.Information(devState.ToString(), contextstr);
                                    if (devState == DeviceStates.UI_CONNECTED)
                                    {
                                        //await DoUIConnection(deviceClient, sslStream, pp.SerialNumber);
                                        await DoUIConnection(deviceClient, sslStream, service_tag, contextstr);
                                    }
                                    else if (devState == DeviceStates.ERROR_DOWNLOAD)
                                    {
                                        Thread.Sleep(2000);
                                        byte[] tmpend_b = System.Text.ASCIIEncoding.ASCII.GetBytes("Y1\xd");
                                        CDPLogger.Information("sent connected (Y1) to device", contextstr);
                                        await sslStream.WriteAsync(tmpend_b).ConfigureAwait(false);
                                        await sslStream.FlushAsync().ConfigureAwait(false);

                                        await DoErrorDownload(deviceClient, sslStream, service_tag, contextstr);
                                        Thread.Sleep(2000);
                                        byte[] tmpend_b2 = System.Text.ASCIIEncoding.ASCII.GetBytes("Y0\xd");
                                        CDPLogger.Information("sent disconnected (Y0) to device", contextstr);
                                        await sslStream.WriteAsync(tmpend_b2).ConfigureAwait(false);
                                        await sslStream.FlushAsync().ConfigureAwait(false);
                                    }
                                    else if (devState == DeviceStates.USAGESTATS_DOWNLOAD)
                                    {
                                        Thread.Sleep(2000);
                                        byte[] tmpend_b = System.Text.ASCIIEncoding.ASCII.GetBytes("Y1\xd");
                                        CDPLogger.Information("sent connected (Y1) to device", contextstr);
                                        await sslStream.WriteAsync(tmpend_b).ConfigureAwait(false);
                                        await sslStream.FlushAsync().ConfigureAwait(false);

                                        await DoUsageStatsDownload(deviceClient, sslStream, service_tag, contextstr);
                                        Thread.Sleep(2000);
                                        byte[] tmpend_b2 = System.Text.ASCIIEncoding.ASCII.GetBytes("Y0\xd");
                                        CDPLogger.Information("sent disconnected (Y0) to device", contextstr);
                                        await sslStream.WriteAsync(tmpend_b2).ConfigureAwait(false);
                                        await sslStream.FlushAsync().ConfigureAwait(false);
                                    }
                                }
                                else
                                {
                                    // do nothing close connection
                                    pkt_invalid = true;
                                }
                                // otherwise the system exits
                            }
                            else
                            {
                                pkt_invalid = true;
                            }
                            if (pkt_invalid)
                            {
                                //check to see if a ui needs connection to this device
                                CDPLogger.Error($"Invalid Packet received. {recv_pkt_str}", contextstr);
                                string [] inv_pkt_rsp = DeviceBLL.SetInvalidPingData(recv_pkt_str.Trim(), contextstr);
                                if (inv_pkt_rsp == null)
                                {
                                    return; // cannot do anything; wait for next ping
                                }
                                string[] pp_arr = recv_pkt_str.Trim().Split(';', StringSplitOptions.RemoveEmptyEntries);
                                // pp_arr[0] is imei number, pp_arr[1] is serial number
                                // servicetag values are bdser<serial number> or bdimei<imei number>
                                string bd_servicetag = string.Empty;
                                string bd_dev_resp = string.Empty;
                                string bdid_search_tag = "bdid" + inv_pkt_rsp[1].Trim();
                                if ( (inv_pkt_rsp[0].ToLower().Equals("success")) && (uiClientDict.ContainsKey(bdid_search_tag) ) )
                                {
                                    bd_servicetag = bdid_search_tag;
                                    bd_dev_resp = "0;0;0;0;0;0;1;0";
                                }
                                else if (uiClientDict.ContainsKey("bdimei" + pp_arr[0].Trim()))
                                {
                                    bd_servicetag = "bdimei" + pp_arr[0].Trim();
                                    bd_dev_resp = "0;0;0;0;0;0;1;0";
                                }
                                else if (uiClientDict.ContainsKey("bdser" + pp_arr[1].Trim()))
                                {
                                    bd_servicetag = "bdser" + pp_arr[1].Trim();
                                    bd_dev_resp = "0;0;0;0;0;0;1;0";
                                }
                                else
                                {
                                    // do nothing
                                }
                                if ((bd_servicetag != string.Empty) && (bd_dev_resp != string.Empty))
                                {
                                    // send response
                                    CDPLogger.Information(string.Format("[invalid pkt recv]: sent to device: <{0}>", bd_dev_resp), contextstr);
                                    SendToClientString(sslStream, bd_dev_resp);
                                    await DoUIConnection(deviceClient, sslStream, bd_servicetag, contextstr);
                                }
                            }
                        }
                    }
                }
            }
            catch (AggregateException agge)
            {
                CDPLogger.Error($"Exception: {agge.Message}", contextstr);
                if (agge.InnerExceptions != null)
                {
                    foreach (Exception excp_i in agge.InnerExceptions)
                    {
                        CDPLogger.Error($"Inner exception: {excp_i.Message}", contextstr);
                    }
                }
                CDPLogger.Error("DeviceProtocolAsync: Authentication failed - closing the connection.", contextstr);
            }
            catch (AuthenticationException ae)
            {
                CDPLogger.Error($"Exception: {ae.Message}", contextstr);
                if (ae.InnerException != null)
                {
                    CDPLogger.Error($"Inner exception: {ae.InnerException.Message}", contextstr);
                }
                CDPLogger.Error("DeviceProtocolAsync: Authentication failed - closing the connection.", contextstr);
                //sslStream.Close();
                //client.Close();
                //return;
            }
            catch (Exception e)
            {
                CDPLogger.Error(e, "DeviceProtocolAsync", contextstr);
            }
            finally
            {
                available_sslstreams.Remove(sslStream);
                sslStream.Close();
                CDPLogger.Information("Device: closing device client", contextstr);
                deviceClient.Close();
                deviceClient.Client.Close();
                deviceClient.Dispose();
                //deviceClient = null;
            }
        }

        #region protocol_funcs
        /// <summary>
        /// This function is used by the function <c>ProcessRecvByteRegex</c> to collect data between multiple 
        /// segments during processing of received packet.
        /// It adds data to the StringBuilder that is the second argument to the function.
        /// </summary>
        /// <param name="add_rem">Add this string to the existing data in the StringBuilder instance</param>
        /// <param name="sb_rem">The StringBuilder instance that is used to append the data</param>
        private static void AddToRemaining(string add_rem, StringBuilder sb_rem)
        {
            CDPLogger.Verbose($"Remaining: {add_rem}");
            sb_rem.Append(add_rem);
        }


        /// <summary>
        /// This function parses received data based on the known basic format.
        /// It assumes start of the valid packet in data as an IMEI number which could be 15 digits or 17 digits long.
        /// and looks for end of packet as 0x0D or the "\r" character.
        /// This function handles partitioned data packets and appends them to provide a valid packet for post processing.
        /// </summary>
        /// <param name="recvstr">raw string data read from device connection</param>
        /// <param name="recvd_msgs">list of parsed string</param>
        /// <param name="sb_rem">object that contains data not processed in the current step. </param>
        private static void ProcessRecvByteRegex(string recvstr, List<string> recvd_msgs, StringBuilder sb_rem)
        {
            var regex_pattern_start = @"\d{17}|\d{15}";
            //var regex_pattern_end = @"\r";
            int tmp_idx;
            //int tmp_len;
            var rx_start = new Regex(regex_pattern_start);
            //var rx_end = new Regex(regex_pattern_start);
            string proc_recvstr;
            if (sb_rem.Length > 0)
            {
                proc_recvstr = sb_rem.ToString() + recvstr;
                sb_rem.Clear();
            }
            else
            {
                proc_recvstr = recvstr;
            }
            MatchCollection mcs_start = rx_start.Matches(proc_recvstr);
            if (mcs_start.Count == 1)
            {
                tmp_idx = proc_recvstr.IndexOf('\r', mcs_start[0].Index + mcs_start[0].Length);
                if (tmp_idx == -1)
                {
                    // not found \r, append to remaining
                    AddToRemaining(proc_recvstr[mcs_start[0].Index..], sb_rem);
                }
                else if ((tmp_idx + 1) == proc_recvstr.Length)
                {
                    // no remaining data
                    recvd_msgs.Add(proc_recvstr[mcs_start[0].Index..]);
                }
                else
                {
                    // there is remaining data
                    recvd_msgs.Add(proc_recvstr[mcs_start[0].Index..tmp_idx]);
                    AddToRemaining(proc_recvstr[(tmp_idx + 1)..], sb_rem);
                }
            }
            else if (mcs_start.Count > 1)
            {
                List<int> check_len = new List<int>();
                for (int jj = 1; jj < mcs_start.Count; jj++)
                {
                    check_len.Add(mcs_start[jj].Index - mcs_start[jj - 1].Index);
                }
                check_len.Add(proc_recvstr.Length - mcs_start[^1].Index);
                List<int> cr_idx = new List<int>();
                for (int kk = 0; kk < mcs_start.Count; kk++)
                {
                    cr_idx.Add(proc_recvstr.IndexOf('\r', mcs_start[kk].Index, check_len[kk]));
                }
                for (int jj = 0; jj < cr_idx.Count; jj++)
                {
                    if (cr_idx[jj] > 15)
                    {
                        recvd_msgs.Add(proc_recvstr[mcs_start[jj].Index..cr_idx[jj]]);
                    }
                    else
                    {
                        // discarding data
                        CDPLogger.Error($"discarding : {proc_recvstr.Substring(mcs_start[jj].Index, check_len[jj])}");
                    }
                }
                if (cr_idx[^1] == -1)
                {
                    // add last bit to remaining
                    AddToRemaining(proc_recvstr[mcs_start[^1].Index..], sb_rem);
                }
            }
            else
            {
                AddToRemaining(proc_recvstr, sb_rem);
            }
        }

        #endregion // protocol_funcs

        #region list_funcs
        //private void AddDeviceSerialNumber(string dev_serial_num_str)
        //{
        //    lock (lock_list)
        //    {
        //        if (!devicesPinged.Contains(dev_serial_num_str))
        //        {
        //            devicesPinged.Add(dev_serial_num_str);
        //        }
        //    }
        //}

        //private string GetAvailableDevices()
        //{
        //    StringBuilder sb = new StringBuilder();
        //    lock(lock_list)
        //    {
        //        foreach (string devs in devicesPinged)
        //        {
        //            sb.Append(string.Format("{0}\n", devs));
        //        }
        //    }
        //    return sb.ToString();
        //}

        //private byte[] GetAvailableDevicesAsBytes()
        //{
        //    MemoryStream ms = new MemoryStream();
        //    lock (lock_list)
        //    {
        //        foreach (string devs in devicesPinged)
        //        {
        //            ms.Write(System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("{0}\n", devs)));
        //        }
        //    }
        //    return ms.ToArray();
        //}

        //private bool IsDeviceAvaialble(string dev_ser_num_str)
        //{
        //    bool found = false;
        //    lock(lock_list)
        //    {
        //        found = devicesPinged.Contains(dev_ser_num_str);
        //    }
        //    return found;
        //}

        #endregion // list_funcs

        #region util_funcs
        /// <summary>
        /// This function is used to confirm writability and then write bytes to the underlying sslstream
        /// </summary>
        /// <param name="net_stream">the stream to write to</param>
        /// <param name="data_send">data to be written to the stream</param>
        public void SendToClient(SslStream net_stream, byte[] data_send)
        {
            if (available_sslstreams.Contains(net_stream))
            {
                if (net_stream.CanWrite)
                {
                    CDPLogger.Verbose("canwrite succ: sent data to device");
                    net_stream.Write(data_send);
                    //await net_stream.WriteAsync(data_send).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// This function is used to write string to the underlying sslstream
        /// </summary>
        /// <param name="net_stream">The stream to write to</param>
        /// <param name="send_str">The string data to be written to the stream</param>
        public void SendToClientString(SslStream net_stream, string send_str)
        {
            byte[] send_b = System.Text.ASCIIEncoding.ASCII.GetBytes(send_str);
            SendToClient(net_stream, send_b);
        }

        #endregion //util_funcs

    }

    //class ClientUnit
    //{
    //    TcpClient _client;
    //    readonly bool _ownsClient;

    //    public ClientUnit(TcpClient client, bool ownsClient)
    //    {
    //        _client = client;
    //        _ownsClient = ownsClient;
    //    }

    //    public async Task ProtocolAsync()
    //    {
    //        try
    //        {
    //            using (var stream = _client.GetStream())
    //            {
    //                byte[] data = new byte[2048] ;
    //                int bytes_rd;
    //                while (true)
    //                {
    //                    bytes_rd = await stream.ReadAsync(data, 0, 2048);
    //                    if (bytes_rd > 0)
    //                    {
    //                        int current_index = 0;
    //                        current_index += bytes_rd;
    //                        // process data and create response here TODO
    //                        CDPLogger.Verbose("Received bytes len => " + bytes_rd.ToString());
    //                        byte[] send_data = new byte[bytes_rd + current_index.ToString().Length + 1];
    //                        System.Array.Copy(data, 0, send_data, 0, current_index);
    //                        byte[] tmp_b = System.Text.ASCIIEncoding.ASCII.GetBytes(current_index.ToString());
    //                        System.Array.Copy(tmp_b, 0, send_data, current_index, tmp_b.Length);;
    //                        send_data[current_index + tmp_b.Length] = 10;
    //                        CDPLogger.Verbose(string.Format("transmitting bytes len {0} \n sent {1}", send_data.Length,
    //                            BitConverter.ToString(send_data)));
    //                        await stream.WriteAsync(send_data, 0, send_data.Length).ConfigureAwait(false);
    //                        await stream.FlushAsync().ConfigureAwait(false);
    //                    }
    //                    else
    //                    {
    //                        break;
    //                    }
    //                }
    //                //while (!((data = await sr.ReadAsync().ConfigureAwait(false)).Equals("exit", StringComparison.OrdinalIgnoreCase)))
    //                //{
    //                //    await sw.WriteLineAsync(data).ConfigureAwait(false);
    //                //    await sw.FlushAsync().ConfigureAwait(false);
    //                //}
    //                //await sw.WriteLineAsync("Hi. This is x2 TCP/IP easy-to-use server").ConfigureAwait(false);
    //                //await sw.FlushAsync().ConfigureAwait(false);
    //                //var data = default(string);
    //            }
    //        }
    //        finally
    //        {
    //            if (_ownsClient && _client != null)
    //            {
    //                if (_client.Connected)
    //                {
    //                    _client.Close();
    //                }
    //                (_client as IDisposable).Dispose();
    //                _client = null;
    //            }
    //        }
    //    }
    //}


}
