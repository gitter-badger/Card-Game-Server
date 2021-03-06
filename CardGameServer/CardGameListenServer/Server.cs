﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using CardGameServer;
using CardProtocolLibrary;

namespace CardGameListenServer
{
    /// <summary>
    /// A listen server that handles initial connections,
    /// starts the Game class when 2 players connect.
    /// </summary>
    public static class Server
    {
        public const int DefaultPort = 4020;
        private static TcpListener _listener;
        private static Thread _listenThread;
        public static readonly List<Client> Clients = new List<Client>();
        public static readonly List<Player> Players = new List<Player>(); 

        /// <summary>
        /// Starts the game server, loads in the game data
        /// </summary>
        /// <param name="port">Leave as -1 to use default port</param>
        public static void Start(int port = -1)
        {
            if (port == -1)
            {
                port = DefaultPort;
            }


            _listener = new TcpListener(IPAddress.Any, port);
            _listenThread = new Thread(ListenForClients);
            _listenThread.Start();
            Console.WriteLine("Started listening for clients");
        }

        private static void ListenForClients()
        {
            _listener.Start();

            while (true)
            {
                // Blocks until a client has connected to the server
                var client = _listener.AcceptTcpClient();
                Console.WriteLine("Client connected ({0})", client.Client.LocalEndPoint);
                // Create a thread to handle communication 
                var clientThread = new Thread(HandleClientComm);
                clientThread.Start(client);
            }
        }

        internal static void ForceClose(Client client, GameDataAction Error)
        {
            if (client.RawClient.Connected)
            {
                client.Writer.SendAction(Error);
            }
        }

        private static async void HandleClientComm(object connection)
        {
            var tcpClient = (TcpClient)connection;
            // Create an instance of our custom client handling object
            var client = new Client(tcpClient);
            Clients.Add(client);
            var player = new Player
            {
                DataWriter = client.Writer
            };
            client.Player = player;
            Players.Add(player);
            if (Players.Count == 2)
            {
                Game.StartGame(Players);
            }

            // Initial Handshake as per protocol s2.0 should start with the
            //  protocol version to make sure client and server are compatible
            client.Writer.SendAction(GameAction.Meta, new Dictionary<string, GameData> { {"protocol", GameActionWriter.PROTOCOL_VERSION.ToString()}});
            var phase = ConnectionPhase.Handshake;
            while (tcpClient.Connected)
            {
                var line = await client.Reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line))
                {
                    // This happens when the client disconnects unexpectedly
                    break;
                }
                var input = new GameDataAction(line);

                Console.WriteLine($"--> Recieved: {input.Action}");
                foreach (var kvp in input.Data)
                {
                    Console.WriteLine($"   + '{kvp.Key}' => {kvp.Value}");
                }
                var handled = false;
                if (input.Action == GameAction.Ping)
                {
                    // Network Protocol s2.1
                    if (input.Data["counter"] == (++client.PingCounter))
                    {
                        client.Writer.SendAction(GameAction.Ping,
                            new Dictionary<string, GameData> {{"counter", (++client.PingCounter)}});
                    }
                    else
                    {
                        // Use ForceClose here? 
                        // Should never occur -- just incase!
                        client.Writer.SendAction(GameAction.Error,
                            new Dictionary<string, GameData> 
                                {
                                    { "code", ErrorCode.PingMismatch },
                                    { "message", $"Server was Expecting: {client.PingCounter}, Client Sent: {input.Data["counter"]}" }
                                });
                    }
                    handled = true;
                }

                if (input.Action == GameAction.Meta)
                {
                    if (phase == ConnectionPhase.Handshake)
                    {
                        if (input.Data.ContainsKey("protocol"))
                        {
                            if (input.Data["protocol"] != GameActionWriter.PROTOCOL_VERSION.ToString())
                            {
                                ForceClose(client, new GameDataAction(GameAction.Error, new Dictionary<string, GameData>
                                {
                                    {"code", ErrorCode.VersionMismatch},
                                    {
                                        "message",
                                        $"Server version: {GameActionWriter.PROTOCOL_VERSION} - Client Version: {input.Data["protocol"]}"
                                    }
                                }));
                                handled = true;
                            }
                        }
                        if (input.Data.ContainsKey("name"))
                        {
                            client.Player.Name = input.Data["name"];
                            handled = true;
                            phase = ConnectionPhase.Setup;
                            client.Writer.SendAction(GameAction.Meta, new Dictionary<string, GameData>
                            {
                                {"phase", ConnectionPhase.Setup}
                            });
                        }
                    }
                }

                if (!handled)
                {
                    Game.Board.RecieveCommand(player, input);
                }
            }
        }
    }
}
