﻿using System.Net;
using System.Threading;
using TrueCraft.Core.World;
using TrueCraft.Core.TerrainGen;
using TrueCraft.Core.Logging;
using TrueCraft.API.Logging;
using TrueCraft.API.Server;
using System.IO;
using TrueCraft.Commands;
using TrueCraft.API.World;
using System;
using TrueCraft.Core;
using TrueCraft.API;
using YamlDotNet.Serialization;

namespace TrueCraft
{
    public class Program
    {
        public static Configuration Configuration;

        public static CommandManager CommandManager;

        public static void Main(string[] args)
        {
            if (File.Exists("config.yaml"))
            {
                var deserializer = new Deserializer();
                Configuration = deserializer.Deserialize<Configuration>(File.OpenText("config.yaml"));
            }
            else
            {
                // Save default configuration
                Configuration = new Configuration();
                var serializer = new Serializer();
                using (var writer = new StreamWriter("config.yaml"))
                    serializer.Serialize(writer, Configuration);
            }
            // TODO: Make this more flexible
            var server = new MultiplayerServer();
            IWorld world;
            try
            {
                // TODO: Save and load levels, with seeds and everything
                world = World.LoadWorld("world");
                world.ChunkProvider = new StandardGenerator();
            }
            catch
            {
                world = new World("default", new StandardGenerator());
                world.Save("world");
            }
            server.AddWorld(world);
            server.AddLogProvider(new ConsoleLogProvider(LogCategory.Notice | LogCategory.Warning | LogCategory.Error | LogCategory.Debug));
            #if DEBUG
            server.AddLogProvider(new FileLogProvider(new StreamWriter("packets.log", false), LogCategory.Packets));
            #endif
            CommandManager = new CommandManager();
            server.ChatMessageReceived += HandleChatMessageReceived;
            server.Start(new IPEndPoint(IPAddress.Any, 25565));
            while (true)
            {
                Thread.Sleep(1000 * 30); // TODO: Allow users to customize world save interval
                foreach (var w in server.Worlds)
                {
                    w.Save();
                }
            }
        }

        static void HandleChatMessageReceived(object sender, ChatMessageEventArgs e)
        {
            if (e.Message.StartsWith("/"))
            {
                e.PreventDefault = true;
                var messageArray = e.Message.TrimStart('/')
                    .Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
                CommandManager.HandleCommand(e.Client, messageArray[0], messageArray);
                return;
            }
        }
    }
}