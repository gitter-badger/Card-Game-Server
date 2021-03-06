﻿using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CardGameListenServer;
using CardProtocolLibrary;

namespace CardGameTestUI
{
    public partial class frmMain : Form
    {
        private TcpClient rawSocket;
        private Client client;

        private const string IP = "localhost";
        private const int port = 4020;
        private int _pingCounter;

        public frmMain()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
        }

        public void SendAction(GameAction action, Dictionary<string, GameData> data)
        {
            client.Writer.SendAction(action, data);
            lbSent.Items.Add($"{action} - {data}");
        }

        private void btnMain_Click(object sender, EventArgs e)
        {
            try
            {
                rawSocket = new TcpClient(IP, port);
                client = new Client(rawSocket);

                SendAction(GameAction.Meta, new Dictionary<string, GameData>
                {
                    {"name", txtName.Name},
                    {"protocol", GameActionWriter.PROTOCOL_VERSION.ToString()}
                });

                tmrPing.Enabled = true;

                Task.Factory.StartNew(() => GetData(client), TaskCreationOptions.LongRunning);
            }
            catch (Exception ex)
            {
                lbRecieved.Items.Add(ex.Message);
                if (rawSocket != null && rawSocket.Connected)
                {
                    rawSocket.Close();
                }
            }
        }

        private void AddLine(string s)
        {
            if (lbRecieved.InvokeRequired)
            {
                lbRecieved.Invoke((MethodInvoker)(() => lbRecieved.Items.Add(s)));
            }
            else
            {
                lbRecieved.Items.Add(s);
            }
        }

        private void AddLine(GameDataAction input)
        {
            var sb = new StringBuilder();
            sb.Append($"{input.Action} : ");
            foreach (var kvp in input.Data)
            {
                sb.Append($"['{kvp.Key}' = {kvp.Value}] ");
            }
            AddLine(sb.ToString());
        }

        private async void GetData(Client c)
        {
            while (c.RawClient.Connected)
            {
                var line = await c.Reader.ReadLineAsync();
                var input = new GameDataAction(line);
                AddLine(input);

                if (input.Action == GameAction.Ping)
                {
                    if (input.Data["counter"].Int() == (++_pingCounter))
                    {
                        AddLine("Ping Success.");
                    }
                    else
                    {
                        AddLine("WARNING: Ping Failure!!!!!!");
                    }
                }
            }
            lbRecieved.Items.Add("Disconnected.");
        }

        private void tmrPing_Tick(object sender, EventArgs e)
        {
            if (rawSocket.Connected)
            {
                SendAction(GameAction.Ping, new Dictionary<string, GameData> { {"counter", (++_pingCounter) } });
            }
        }
    }
}
