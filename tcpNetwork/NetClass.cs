using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;

namespace tcpNet
{
    public class tcpNetwork
    {
        public int port;
        private Random identity;
        public bool keepHistory = false;
        public List<Message> messageHistory = new List<Message>();
        public ClientList CList = new ClientList();
        public string HostName;
        public static string MyIP;
        //Basic Constructor
        public tcpNetwork(int port, bool keepHistory, string HostName)
        {
            this.port = port;
            this.keepHistory = keepHistory;
            this.HostName = HostName;
            this.identity = new Random();
        }

        //Constructor with Console Output
        public tcpNetwork(int port, bool keepHistory, string HostName, System.IO.TextWriter ConsoleOUT)
        {
            this.port = port;
            this.keepHistory = keepHistory;
            this.HostName = HostName;
            this.identity = new Random();
            Console.SetOut(ConsoleOUT);
        }

        public class Listener
        {
            public int port;
            public TcpListener tcpl;
            public Timer refreshTimer;
            public int bufferSize;


            //Ad Hoc Constructor
            public Listener(int port, int bufferSize)
            {
                this.port = port;
                this.bufferSize = bufferSize;
                if (this.bufferSize < 64) { bufferSize = 64; }
                try
                {
                    IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, port);
                    this.tcpl = new TcpListener(endPoint);
                    this.tcpl.Server.ReceiveTimeout = 3000;
                    this.tcpl.Start();
                    Console.WriteLine("Listener Started");
                    refreshTimer = null;

                }
                catch (SocketException se)
                {
                    Console.WriteLine("Failed to Connect: " + se.Message);
                }
            }

            //Constructor with Timer
            public Listener(Timer refreshTimer, int port, int bufferSize)
            {
                this.port = port;
                this.bufferSize = bufferSize;
                if (this.bufferSize < 64) { bufferSize = 64; }
                try
                {
                    IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, port);
                    this.tcpl = new TcpListener(endPoint);
                    this.tcpl.Server.ReceiveTimeout = 3000;
                    this.tcpl.Start();
                    Console.WriteLine("Listener Started");
                    refreshTimer.Start();

                }
                catch (SocketException se)
                {
                    Console.WriteLine("Failed to Connect: " + se.Message);
                }
            }

            public void Close()
            {
                if (tcpl != null)
                {
                    if (refreshTimer != null)
                    {
                        refreshTimer.Stop();
                    }
                    tcpl.Stop();
                    Console.WriteLine("Listener Stopped");
                }
            }

            public bool IsClientWaiting()
            {
                return this.tcpl.Pending();
            }

            public string Listen()
            {
                string rawdata = null;
                TcpClient Client = null;
                byte[] Buffer = new byte[bufferSize];
                Client = this.tcpl.AcceptTcpClient();
                string sender = Client.Client.RemoteEndPoint.ToString();

                if (Client.Client.Available != 0)
                {
                    NetworkStream stream = Client.GetStream();
                    int byteCount = stream.Read(Buffer, 0, Buffer.Length);
                    rawdata = UTIL.streamTools.Unzip(Buffer);
                }
                return rawdata;
            }

        }

        //Parse Raw Data
        public Message ParseData(string rawdata)
        {
            try
            {
                if (rawdata.Substring(0, 2) == "S|" && rawdata.Substring(rawdata.Length - 2, 2) == "|E")
                {
                    string[] payload = rawdata.Split('|');
                    Message retMessage = new Message(payload[1], payload[2], payload[3], payload[4]);
                    if (keepHistory)
                    {
                        messageHistory.Add(retMessage);
                    }
                    return retMessage;
                }
                else
                {
                    Console.WriteLine("Error-Message did not contain START/END headers: \"{0}\"", rawdata);
                    return null;
                }
            }
            catch (ArgumentOutOfRangeException oor)
            {
                Console.WriteLine("Error-Data packet recieved was too short to parse: \"{0}\"", rawdata);
                return null;
            }


        }


        public void SendData(string IP, string data, string type)
        {
            TcpClient T = null;
            IPEndPoint IPE = null;
            NetworkStream N = null;

            int msgId = identity.Next(9999999);

            string str_msgId = msgId.ToString().PadLeft(7, '0');

            string message = "S|" + str_msgId + "|" + type + "|" + GetMyIP().ToString() + "|" + data.Replace("|", "") + "|E";
            
            try
            {
                T = new TcpClient();
                IPE = new IPEndPoint(IPAddress.Parse(IP), this.port);
                try
                {
                    T.Connect(IPE);
                    N = T.GetStream();
                    byte[] Buffer = UTIL.streamTools.Zip(message);
                    N.Write(Buffer, 0, Buffer.Length);
                    N.Flush();
                    //Log to Message History
                    messageHistory.Add(new Message(str_msgId, type, GetMyIP().ToString(), data.Replace("|", "")));
                }
                catch (SocketException se)
                {
                    Console.WriteLine("Error: " + se.Message);
                    if (CList.GetClientByIP(IP) != null)
                    {
                        Client badClient = CList.GetClientByIP(IP);
                        Console.WriteLine("{0} is no longer with us.",badClient.NAME);
                        CList.Clients.Remove(badClient);
                    }
                }
            }
            finally
            {
                if (N != null)
                {
                    N.Dispose();
                }
                if (T != null)
                {
                    T.Dispose();
                }
            }

        }

        public void SendData(List<string> IPList, string data, string type)
        {
            TcpClient T = null;
            IPEndPoint IPE = null;
            NetworkStream N = null;
            foreach (string IP in IPList)
            {
                if (IP == GetMyIP().ToString())
                {
                    continue;
                }
                int msgId = identity.Next(9999999);
                string str_msgId = msgId.ToString().PadLeft(7, '0');
                string message = "S|" + str_msgId + "|" + type + "|" + GetMyIP().ToString() + "|" + data.Replace("|", "") + "|E";

                try
                {
                    T = new TcpClient();
                    IPE = new IPEndPoint(IPAddress.Parse(IP), this.port);
                    try
                    {
                        T.Connect(IPE);
                        N = T.GetStream();
                        byte[] Buffer = UTIL.streamTools.Zip(message);
                        N.Write(Buffer, 0, Buffer.Length);
                        N.Flush();
                    }
                    catch (SocketException se)
                    {
                        Console.WriteLine("Error: " + se.Message);
                    }
                }
                finally
                {
                    if (N != null)
                    {
                        N.Dispose();
                    }
                    if (T != null)
                    {
                        T.Dispose();
                    }
                }
            }

        }

        public void SendAck(Message M)
        {
            SendData(M.Sender, M.Id, "A");
        }

        public static IPAddress GetMyIP()
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint.Address;
            }
        }

        public class Message
        {
            public string Id { get; set; }
            public string Type { get; set; }
            public string Sender { get; set; }
            public string Data { get; set; }

            public Message(string Id, string Type, string Sender, string Data)
            {
                this.Id = Id;
                this.Type = Type;
                this.Sender = Sender;
                this.Data = Data;
            }
        }



        public class Client
        {
            public string IP;
            public string NAME;
            public bool[] ClientAttr = new bool[10];
            public List<Message> MESSAGES = new List<Message>();

            public Client(string IP,string NAME)
            {
                this.IP = IP;
                this.NAME = NAME;
            }
            
        }

        public class ClientList : IEnumerable
        {
            public List<Client> Clients;

            public ClientList()
            {
                Clients = new List<Client>();
            }

            public bool IsNewClient(Message M)
            {
                foreach (Client C in this.Clients)
                {
                    if (C.IP == M.Sender)
                    {
                        return false;
                    }
                }
                    return true;
            }
            public Client AddNewClient(string IP, String Name)
            {
                if (this.GetClientByIP(IP) != null)
                {
                    this.Clients.Remove(GetClientByIP(IP));
                }
                this.Clients.Add(new Client(IP, Name));
                return this.Clients[this.Clients.Count - 1];

            }
            public Client GetClientByMessage(Message m)
            {
                if (!(IsNewClient(m)))
                {
                    foreach (Client C in Clients)
                    {
                        if (C.IP == m.Sender)
                        {
                            return C;
                        }
                    }
                }
            return null;
                
            }
            public Client GetClientByIP(string IP)
            {
                foreach (Client C in Clients)
                {
                    if (C.IP == IP)
                    {
                        return C;
                    }
                }
                
                return null;
            }
            public List<string> GetAllClientIPs()
            {
                List<string> IPList = new List<string>();
                foreach (Client C in Clients)
                {
                    if (C.IP == GetMyIP().ToString())
                    {
                        continue;
                    }
                    IPList.Add(C.IP);
                }

                return IPList;
            }
            public List<string> GetAllClientIPsByAttr(bool boolMatch, int attrNum)
            {
                List<string> IPList = new List<string>();
                foreach (Client C in Clients)
                {
                    if (C.ClientAttr[attrNum] == boolMatch)
                    {
                        IPList.Add(C.IP);
                    }
                }
                return IPList;
            }
            public IEnumerator GetEnumerator()
            {
                return new addrEnum(this);
            }
            private class addrEnum : IEnumerator
            {
                private int position = -1;
                private ClientList instance;
                public addrEnum(ClientList inst)
                {
                    this.instance = inst;
                }

                public object Current
                {
                    get { return instance.Clients[position]; }
                }
                public bool MoveNext()
                {
                                position++;
                                return (position < instance.Clients.Count);
                }
                public void Reset()
                {
                    position = -1;
                }
            }
        }

    }

}

namespace UTIL
{
    public static class streamTools
    {
        public static void CopyTo(Stream src, Stream dest)
        {
            byte[] bytes = new byte[4096];

            int cnt;

            while ((cnt = src.Read(bytes, 0, bytes.Length)) != 0)
            {
                dest.Write(bytes, 0, cnt);
            }
        }

        public static byte[] Zip(string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str);

            using (var msi = new MemoryStream(bytes))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(mso, CompressionMode.Compress))
                {
                    //msi.CopyTo(gs);
                    CopyTo(msi, gs);
                }

                return mso.ToArray();
            }
        }

        public static string Unzip(byte[] bytes)
        {
            using (var msi = new MemoryStream(bytes))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(msi, CompressionMode.Decompress))
                {
                    //gs.CopyTo(mso);
                    CopyTo(gs, mso);
                }

                return Encoding.UTF8.GetString(mso.ToArray());
            }
        }
    }
}