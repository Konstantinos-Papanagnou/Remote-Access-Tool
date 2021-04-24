using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using System.Xml;

namespace Client
{
    class Program
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        private static readonly Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private static int port = 10200;
        private static string ipAddress;
        private static IPAddress IPADRRESS;

        static void Main(string[] args)
        {
            SetReadData();
            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_HIDE);
            ConnectToServer();
            string hostname = Dns.GetHostName();
            IPHostEntry ipe = Dns.GetHostEntry(hostname);
            IPAddress[] addresses = ipe.AddressList;
            string ipv4 = GetIpv4Address(addresses);
            string username = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            sendCommand(ipv4, hostname, username);
            ReceiveLoop();
        }
        private static void CreateXml(string path)
        {
            XmlTextWriter xwriter = new XmlTextWriter(path, Encoding.UTF8);
            xwriter.Formatting = Formatting.Indented;
            xwriter.WriteStartElement("Data");// <Data>
            xwriter.WriteStartElement("FirstStart"); //<FirstStart>
            xwriter.WriteString("false"); //>false<..
            xwriter.WriteEndElement(); //</FirstStart>
            xwriter.WriteStartElement("IpAddress"); // <IpAddress>
            xwriter.WriteString("");
            xwriter.WriteEndElement(); //</IpAddress>
            xwriter.WriteEndElement(); //</Data>
            xwriter.Close();
        }
        private static void SetReadData()
        {
            CommonApplicationData data = new CommonApplicationData("Pap Industries", "Client");
            if(!File.Exists(data.ApplicationFolderPath + "\\data.xml"))
            {
                CreateXml(data.ApplicationFolderPath + "\\data.xml");
            }

            XmlDocument doc = new XmlDocument();
            doc.Load(data.ApplicationFolderPath + "\\data.xml");
            XmlNode firstStart = doc.SelectSingleNode("Data/FirstStart");
            XmlNode ip = doc.SelectSingleNode("Data/IpAddress");
            if (firstStart.InnerText == "false")
            {
                Console.WriteLine("Give host ip Address");
                ipAddress = Console.ReadLine();
                while(!IPAddress.TryParse(ipAddress, out IPADRRESS))
                {
                    Console.WriteLine("Invalid ip address! Please try again!");
                    ipAddress = Console.ReadLine();
                }
                firstStart.InnerText = "true";
                ip.InnerText = ipAddress;
                doc.Save(data.ApplicationFolderPath + "\\data.xml");
            }
            ipAddress = ip.InnerText.ToString();
        }

        public static void ConnectCallback(IAsyncResult ar)
        {
            Socket s = (Socket)ar.AsyncState;
            s.EndConnect(ar);
            string hostname = Dns.GetHostName();
            IPHostEntry ipe = Dns.GetHostEntry(hostname);
            IPAddress[] addresses = ipe.AddressList;
            string ipv4 = GetIpv4Address(addresses);
            string username = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            sendCommand(ipv4, hostname, username);
        }

        private static string GetIpv4Address(IPAddress[] ips)
        {
            foreach(var ip in ips)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
            }
            return null;
        }

        private static void ConnectToServer()
        {
            while (!socket.Connected)
            {
                try
                {
                    socket.Connect(new IPEndPoint(IPAddress.Parse(ipAddress), port));
                    Thread.Sleep(1000);
                }
                catch (SocketException) { }

            }
            Console.WriteLine("Connected");

        }

        private static void ReceiveLoop()
        {
            while (true)
            {
                if(socket.Connected)
                    ReceiveResponse();
                Thread.Sleep(1000);
            }
        }

        private static void ReceiveResponse()
        {
            var buffer = new byte[2048];
            int received = socket.Receive(buffer, SocketFlags.None);
            if (received == 0) return;
            var data = new byte[received];
            Array.Copy(buffer, data, received);
            string text = Encoding.UTF8.GetString(data);

            string[] Receiveddata = text.Split('|');

            string command = Receiveddata[0];
            if (command == "dispmsg")
                Commands(command, Receiveddata[1], Receiveddata[2], Receiveddata[3]);
            else if(command == "Kproc")
            {
                Commands(command, Receiveddata[1]);
            }
            else if(command == "Sproc")
            {
                Commands(command, Receiveddata[1]);
            }
            else
                Commands(command);
        }

        private static void sendCommand(string ip, string hostname, string username)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("SID|" + ip + "|" + hostname + "|" + username);
            socket.Send(data);
        }

        private static void Commands(string command, string text = "" , string title = "", string type = "")
        {
            switch (command)
            {
                case "dispmsg":
                    switch (type)
                    {
                        case "Error":
                            MessageBox.Show(text, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                            break;
                        case "Warning":
                            MessageBox.Show(text, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            break;
                        case "None":
                            MessageBox.Show(text, title, MessageBoxButtons.OK, MessageBoxIcon.None);
                            break;
                        case "Exclamation":
                            MessageBox.Show(text, title, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                            break;
                        default:
                            MessageBox.Show(text, title);
                            break;
                    }
                    break;

                case "shutdown":
                    ProcessStartInfo psi = new ProcessStartInfo("shutdown.exe", "/s /t 30");
                    Process.Start(psi);
                    break;
                case "restart":
                    ProcessStartInfo psi2 = new ProcessStartInfo("shutdown.exe", "/r /t 30");
                    Process.Start(psi2);
                    break;
                case "Rproc":
                    //Retrieve all processes
                    Process[] procs = Process.GetProcesses();
                    string[] pid = new string[procs.Count()];
                    string[] procName = new string[procs.Count()];
                    for (int i = 0; i < procs.Count();i++)
                    {
                        pid[i] = procs[i].Id.ToString();
                        procName[i] = procs[i].ProcessName.ToString();
                    }
                    SendProcData(pid, procName);
                    break;
                case "Kproc":
                    try
                    {
                        ProcessStartInfo psi3 = new ProcessStartInfo("taskkill", "/F /PID " + text);
                        Process.Start(psi3);
                    }
                    catch
                    {
                    }
                    break;
                case "Sproc":
                    try
                    {
                        ProcessStartInfo psi4 = new ProcessStartInfo(text);
                        Process.Start(psi4);
                        SendResponse("Successfully started process: " + text);
                    }
                    catch
                    {
                        SendResponse(text + " was not found by the system!");
                    }
                    break;
                case "Disconnect":
                    socket.Disconnect(true);
                    socket.BeginConnect(new IPEndPoint(IPAddress.Parse(ipAddress), port), ConnectCallback, socket);
                    break;
            }
        }

        private static void SendResponse(string status)
        {
            string command = "Status";
            byte[] data = System.Text.Encoding.UTF8.GetBytes(command + "|" + status);
            socket.Send(data);
        }

        private static void SendProcData(string[] PID, string[] procName)
        {
            string cData = "Rproc|";
            for(int i = 0; i < PID.Count(); i++)
            {
                cData += PID[i] + "/" + procName[i] + "|";
            }
            byte[] data = System.Text.Encoding.UTF8.GetBytes(cData);
            socket.Send(data);
        }
    }
}
