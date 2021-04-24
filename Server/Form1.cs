using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Server
{
    public partial class Form1 : Form
    {
        private bool open = false;
        private Socket serverSocket;
        private List<Socket> clients = new List<Socket>();
        private const int port = 10200;
        private const int BufferSize = 2000000;
        private readonly byte[] buffer = new byte[BufferSize];
        private bool closing = false;
        private List<string> ips = new List<string>();
        private List<string> hostnames = new List<string>();
        private List<string> Usernames = new List<string>();
        private string[] pid;
        private string[] ProcNames;
        private int MaxClients = 0;
        private int CurrentClient = 0;
        private bool pidOrder = true;
        private bool atLeastOneClient = false;
        private int high = 5000;
        private int medium = 7000;
        private int low = 10000;
        private bool isPaused = true;
        private bool UpdateOnHigh = false;
        private bool UpdateOnNormal = false;
        private bool UpdateOnLow = false;
        private Thread update;
        private int ClientsCount = 0;

        public Form1()
        {
            InitializeComponent();
            update = new Thread(new ThreadStart(updateProcs));
        }
        private void ListClients()
        {
            listView1.Items.Clear();
            int i = 0;
            foreach (Socket client in clients)
            {
                ListViewItem lvi = new ListViewItem(i.ToString());
                lvi.SubItems.Add(ips[i]);
                lvi.SubItems.Add(hostnames[i]);
                lvi.SubItems.Add(Usernames[i]);
                listView1.Items.Add(lvi);
                i++;
            }
        }

        private void SetupServer()
        {
            if (!open)
            {
                serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));
                serverSocket.Listen(50);
                serverSocket.BeginAccept(AcceptCallBack, null);
                label2.Text = "Active";
                open = true;
            }
        }

        private void AcceptCallBack(IAsyncResult AR)
        {
            Socket socket;

            try
            {
                socket = serverSocket.EndAccept(AR);
                atLeastOneClient = true;
            }
            catch (ObjectDisposedException) { return; }

            clients.Add(socket);
            socket.BeginReceive(buffer, 0, BufferSize, SocketFlags.None, ReceiveCallBack, socket);
            serverSocket.BeginAccept(AcceptCallBack, null);
        }

        private void ReceiveCallBack(IAsyncResult AR)
        {
            if (!closing)
            {
                Socket socket = (Socket)AR.AsyncState;
                int received;
                int i = 0;
                foreach (var s in clients)
                {
                    if(s == socket)
                    {
                        break;
                    }
                    i++;
                }
                try
                {
                    received = socket.EndReceive(AR);
                }
                catch (SocketException)
                {
                    socket.Close();
                    notification.ShowBalloonTip(8000, "Disconnected!", "Client forcefully disconnected from: " + Usernames[i] , ToolTipIcon.Warning);
                    clients.Remove(socket);
                    ips.Remove(ips[i]);
                    hostnames.Remove(hostnames[i]);
                    Usernames.Remove(Usernames[i]);
                    ClientsCount--;
                    listView1.Invoke(new Action(() => listView1.Items.Clear()));
                    int count = 0;
                    foreach(var client in clients)
                    {
                        ListViewItem lvi = new ListViewItem(count.ToString());
                        lvi.SubItems.Add(ips[count].ToString());
                        lvi.SubItems.Add(hostnames[count].ToString());
                        lvi.SubItems.Add(Usernames[count].ToString());
                        listView1.Invoke(new Action(() => listView1.Items.Add(lvi)));
                        count++;
                    }
                    if (clients.Count <= 0)
                        atLeastOneClient = false;
                    return;
                }

                byte[] recBuff = new byte[received];
                Array.Copy(buffer, recBuff, received);
                string text = Encoding.UTF8.GetString(recBuff);

                string[] data = text.Split('|');
                if(data[0] == "SID")
                {
                    string ip = data[1];
                    string hostname = data[2];
                    string username = data[3];
                    ips.Add(ip);
                    hostnames.Add(hostname);
                    Usernames.Add(username);
                    ListViewItem lvi = new ListViewItem(ClientsCount.ToString());
                    lvi.SubItems.Add(ip);
                    lvi.SubItems.Add(hostname);
                    lvi.SubItems.Add(username);
                    notification.ShowBalloonTip(5000, "Connected!", "Created Connection with: " + username, ToolTipIcon.Info);
                    listView1.Invoke(new Action(() => listView1.Items.Add(lvi)));
                    ClientsCount++;
                }
                else if(data[0] == "Rproc") 
                {
                    GetProcData(data);
                }
                else if(data[0] == "Status")
                {
                    MessageBox.Show(data[1], data[0], MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                socket.BeginReceive(buffer, 0, BufferSize, SocketFlags.None, ReceiveCallBack, socket);
            }
        }
        private void CloseAllSockets()
        {
            closing = true;
            int i = 0;
            foreach(Socket socket in clients)
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
                ips.Remove(ips[i]);
                hostnames.Remove(hostnames[i]);
                Usernames.Remove(Usernames[i]);
                i++;
            }
            clients.Clear();
            atLeastOneClient = false;
            serverSocket.Close();
            label2.Text = "Inactive";
            ClientsCount = 0;
        }

        private void StartServerbtn_Click(object sender, EventArgs e)
        {
            closing = false;
            SetupServer();
        }

        private void CloseServer_Click(object sender, EventArgs e)
        {
            listView1.Items.Clear();
            if (open)
            {
                closing = true;
                int i = 0;
                foreach (Socket socket in clients)
                {
                    SendTextCommand("Disconnect", i);
                    ips.Remove(ips[i]);
                    hostnames.Remove(hostnames[i]);
                    Usernames.Remove(Usernames[i]);
                    i++;
                }
                clients.Clear();
                atLeastOneClient = false;
                serverSocket.Close();
                label2.Text = "Inactive";
                ClientsCount = 0;
                open = false;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (open)
                CloseAllSockets();
        }

        private void refreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ListClients();
        }

        private void SendTextCommand(string command, string text, string title, string type)
        {
            foreach(ListViewItem lvi in listView1.CheckedItems)
            {
                Socket socket = clients[Convert.ToInt32(lvi.SubItems[0].Text)];
                byte[] data = System.Text.Encoding.UTF8.GetBytes(command + "|" + text + "|" + title + "|" + type);
                socket.Send(data);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                switch (msgTypecombo.SelectedItem.ToString())
                {
                    case "Error":
                        SendTextCommand("dispmsg", textBox1.Text, textBox2.Text, "Error");
                        break;
                    case "Warning":
                        SendTextCommand("dispmsg", textBox1.Text, textBox2.Text, "Warning");
                        break;
                    case "None":
                        SendTextCommand("dispmsg", textBox1.Text, textBox2.Text, "None");
                        break;
                    case "Exclamation":
                        SendTextCommand("dispmsg", textBox1.Text, textBox2.Text, "Exclamation");
                        break;
                }
            }
            catch { SendTextCommand("dispmsg", textBox1.Text, textBox2.Text, ""); }

            textBox1.Text = "";
            textBox2.Text = "";
        }

        private void Shutdownbtn_Click(object sender, EventArgs e)
        {
            SendTextCommand("shutdown", "", "", "");
        }

        private void Restartbtn_Click(object sender, EventArgs e)
        {
            SendTextCommand("restart", "", "", "");
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if(atLeastOneClient)
                SendTextCommand("Sproc|" + SnprocTextBox.Text, CurrentClient);
        }

        private void tabPage2_Enter(object sender, EventArgs e)
        {
            try
            {
                if (pidOrder)
                {
                    pIDToolStripMenuItem.Image = Properties.Resources.Check;
                    processNameToolStripMenuItem.Image = null;
                }
                else
                {
                    pIDToolStripMenuItem.Image = null;
                    processNameToolStripMenuItem.Image = Properties.Resources.Check;
                }
                MaxClients = clients.Count();
                CurrentClient = 0;
                label15.Text = "UserName: " + Usernames[CurrentClient];
                MxLabel.Text = clients.Count.ToString();
                textBox3.Text = "1";
                SendTextCommand("Rproc", CurrentClient);
            }
            catch
            {
                MessageBox.Show("No Client Connected!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void GetProcData(string[] data)
        {
            pid = new string[data.Count() - 1];
            ProcNames = new string[data.Count() - 1];
            for (int i = 1; i < data.Count(); i++)
            {
                string[] splited_Data = data[i].Split('/');
                if(splited_Data.Count() == 2)
                {
                    pid[i - 1] = splited_Data[0];
                    ProcNames[i - 1] = splited_Data[1];
                }
                else
                {
                    pid[i - 1] = splited_Data[0];
                    ProcNames[i - 1] = string.Empty;
                }
            }
            int[] id = new int[pid.Count()];
            for(int i = 0; i <pid.Count();i++)
            {
                if (pid[i] == string.Empty)
                    pid[i] = "0";
            }
            if (pidOrder)
            {
                for (int i = 0; i < pid.Count(); i++)
                {
                    id[i] = Convert.ToInt32(pid[i]);
                }
                for (int i = 0; i < pid.Count(); i++)
                {
                    for (int j = pid.Count() - 1; j > 0; j--)
                    {
                        if (id[j - 1] > id[j])
                        {
                            int temp = id[j - 1];
                            id[j - 1] = id[j];
                            id[j] = temp;
                            string temp2 = ProcNames[j - 1];
                            ProcNames[j - 1] = ProcNames[j];
                            ProcNames[j] = temp2;
                            string temp3 = pid[j - 1];
                            pid[j - 1] = pid[j];
                            pid[j] = temp3;
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < pid.Count(); i++)
                {
                    for (int j = pid.Count() -1; j > 0; j--)
                    {
                        if (String.Compare(ProcNames[j - 1], ProcNames[j]) < 0)
                        {
                            string temp2 = ProcNames[j - 1];
                            ProcNames[j - 1] = ProcNames[j];
                            ProcNames[j] = temp2;
                            string temp = pid[j - 1];
                            pid[j - 1] = pid[j];
                            pid[j] = temp;
                        }
                    }
                }
            }


            for (int i = 0; i < pid.Count(); i++)
            {
                ListViewItem lvi = new ListViewItem(pid[i].ToString());
                lvi.SubItems.Add(ProcNames[i]);
                listView2.Invoke(new Action( () => listView2.Items.Add(lvi)));
            }
        }
        private void SendTextCommand(string command, int client)
        {
            Socket socket = clients[client];
            byte[] data = System.Text.Encoding.UTF8.GetBytes(command);
            socket.Send(data);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (atLeastOneClient)
            {
                CurrentClient++;
                UpdateUI();
            }
        }


        private void UpdateUI()
        {
            if(CurrentClient == MaxClients)
            {
                CurrentClient = 0;
            }
            else if(CurrentClient < 0)
            {
                CurrentClient = MaxClients - 1;
            }
            textBox3.Text = (CurrentClient + 1).ToString();
            label15.Text = "UserName: " + Usernames[CurrentClient];
            SendTextCommand("Rproc", CurrentClient);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (atLeastOneClient)
            {
                CurrentClient--;
                UpdateUI();
            }
        }

        private void pIDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!pidOrder)
            {
                pidOrder = true;
                pIDToolStripMenuItem.Image = Properties.Resources.Check;
                processNameToolStripMenuItem.Image = null;
                if(atLeastOneClient)
                    Order();
            }
        }

        private void processNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pidOrder)
            {
                pidOrder = false;
                processNameToolStripMenuItem.Image = Properties.Resources.Check;
                pIDToolStripMenuItem.Image = null;
                if(atLeastOneClient)
                    Order();
            }
        }
        private void Order()
        {
            listView2.Items.Clear();
            int[] id = new int[pid.Count()];
            if (pidOrder)
            {
                for (int i = 0; i < pid.Count(); i++)
                {
                    id[i] = Convert.ToInt32(pid[i]);
                }
                for (int i = 0; i < pid.Count(); i++)
                {
                    for (int j = pid.Count() - 1; j > 0; j--)
                    {
                        if (id[j - 1] > id[j])
                        {
                            int temp = id[j - 1];
                            id[j - 1] = id[j];
                            id[j] = temp;
                            string temp2 = ProcNames[j - 1];
                            ProcNames[j - 1] = ProcNames[j];
                            ProcNames[j] = temp2;
                            string temp3 = pid[j - 1];
                            pid[j - 1] = pid[j];
                            pid[j] = temp3;
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < pid.Count(); i++)
                {
                    for(int j = pid.Count() -1; j > 0; j--)
                    {
                        if(String.Compare(ProcNames[j-1], ProcNames[j]) < 0)
                        {
                            string temp2 = ProcNames[j - 1];
                            ProcNames[j - 1] = ProcNames[j];
                            ProcNames[j] = temp2;
                            string temp = pid[j - 1];
                            pid[j - 1] = pid[j];
                            pid[j] = temp;
                        }
                    }
                }
            }

            for (int i = 0; i < id.Count(); i++)
            {
                ListViewItem lvi;
                if (pidOrder)
                {
                    lvi = new ListViewItem(id[i].ToString());
                    lvi.SubItems.Add(ProcNames[i]);
                }
                else
                {
                    lvi = new ListViewItem(pid[i]);
                    lvi.SubItems.Add(ProcNames[i]);
                }

                listView2.Items.Add(lvi);
            }
        }

        private void refreshNowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (atLeastOneClient)
            {
                listView2.Items.Clear();
                SendTextCommand("Rproc", CurrentClient);
            }
        }

        private void textBox3_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.KeyData == Keys.Enter)
            {
                int pos;
                bool attemp = Int32.TryParse(textBox3.Text, out pos);
                if (attemp)
                {
                    if (pos <= MaxClients && pos > 0 && atLeastOneClient)
                    {
                        CurrentClient = pos - 1;
                        listView2.Items.Clear();
                        SendTextCommand("Rproc", CurrentClient);
                    }
                    else
                    {
                        MessageBox.Show("Out of range exception or no client connected!");
                    }
                }
                else
                {
                    MessageBox.Show("Text needs to be number!");
                }
            }
        }

        private void updateProcs()
        {
            if (atLeastOneClient)
            {
                while (!isPaused)
                {
                    SendTextCommand("Rproc", CurrentClient);
                    if (UpdateOnHigh)
                        Thread.Sleep(high);
                    else if (UpdateOnNormal)
                        Thread.Sleep(medium);
                    else
                        Thread.Sleep(low);
                }
            }
            else
            {
                MessageBox.Show("No Client Connected!");
            }
        }

        private void highToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!UpdateOnHigh)
            {
                if(isPaused)
                    update.Start();
                isPaused = false;
                UpdateOnLow = false;
                UpdateOnNormal = false;
                UpdateOnHigh = true;
                MessageBox.Show("Now Updating on High refresh rate!");
            }
            else
            {
                MessageBox.Show("Already updating on high rate!");
            }
        }

        private void normalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!UpdateOnNormal)
            {
                if(isPaused)
                    update.Start();
                isPaused = false;
                UpdateOnLow = false;
                UpdateOnNormal = true;
                UpdateOnHigh = false;
                MessageBox.Show("Now Updating on Normal refresh rate!");
            }
            else
            {
                MessageBox.Show("Already updating on normal rate!");
            }
        }

        private void lowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!UpdateOnLow)
            {
                if (isPaused)
                    update.Start();
                isPaused = false;
                UpdateOnLow = true;
                UpdateOnNormal = false;
                UpdateOnHigh = false;
                MessageBox.Show("Now Updating on Low refresh rate!");
            }
            else
            {
                MessageBox.Show("Already updating on low rate!");
            }
        }

        private void pauseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!isPaused)
            {
                update.Abort();
                isPaused = true;
                UpdateOnLow = false;
                UpdateOnHigh = false;
                UpdateOnNormal = false;
                MessageBox.Show("Refresh rate paused!");
            }
            else
            {
                MessageBox.Show("Already paused!");
            }
        }

        private void killProcessToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach(ListViewItem lvi in listView2.CheckedItems)
            {
                SendTextCommand("Kproc|" + lvi.SubItems[0].Text , CurrentClient);
                listView2.Items.Remove(lvi);
            }
        }

        private void tabPage2_Leave(object sender, EventArgs e)
        {
            listView2.Items.Clear();
            label15.Text = "UserName: ";
            MxLabel.Text = "x";
            textBox3.Text = "";
        }

        private void notification_Click(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                this.Hide();
            }
        }
    }
}
