﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Uchu.Api;
using Uchu.Api.Models;
using Uchu.Auth.Handlers;
using Uchu.Char.Handlers;
using Uchu.Core;
using Uchu.Core.Providers;
using Uchu.World;
using Uchu.World.Handlers;

namespace Uchu.Instance
{
    internal static class Program
    {
        private static Server Server { get; set; }
        
        private static Guid Id { get; set; }
        
        private static ServerType ServerType { get; set; }
        
        private static async Task Main(string[] args)
        {
            if (args.Length != 2)
                throw new ArgumentException("Expected 2 argument.");

            if (!Guid.TryParse(args[0], out var id))
                throw new ArgumentException($"{args[0]} is not a valid GUID");

            Id = id;

            await ConfigureAsync(args[1]).ConfigureAwait(false);

            Logger.Debug($"Process ID: {Process.GetCurrentProcess().Id}");
            
            try
            {
                switch (ServerType)
                {
                    case ServerType.Authentication:
                        await Server.StartAsync(typeof(LoginHandler).Assembly, true);
                        break;
                    case ServerType.Character:
                        await Server.StartAsync(typeof(CharacterHandler).Assembly);
                        break;
                    case ServerType.World:
                        Server.RegisterAssembly(typeof(CharacterHandler).Assembly);
                        await Server.StartAsync(typeof(WorldInitializationHandler).Assembly);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
            
            Logger.Information("Exiting...");

            Console.ReadKey();
        }

        private static async Task ConfigureAsync(string config)
        {
            var serializer = new XmlSerializer(typeof(Configuration));

            if (!File.Exists(config))
            {
                throw new ArgumentException($"{config} config file does not exist.");
            }

            Configuration configuration;
            
            await using (var fs = File.OpenRead(config))
            {
                UchuContextBase.Config = configuration = (Configuration) serializer.Deserialize(fs);
            }
            
            var masterPath = Path.GetDirectoryName(config);

            SqliteContext.DatabasePath = Path.Combine(masterPath, "./Uchu.sqlite");

            var api = new ApiManager(configuration.ApiConfig.Protocol, configuration.ApiConfig.Domain);

            var instance = await api.RunCommandAsync<InstanceInfoResponse>(
                configuration.ApiConfig.Port, $"instance/target?i={Id}"
            ).ConfigureAwait(false);

            if (!instance.Success)
            {
                Logger.Error(instance.FailedReason);

                throw new Exception(instance.FailedReason);
            }

            Server = instance.Info.Type == (int) ServerType.World
                ? new WorldServer(Id)
                : new Server(Id);
            
            Console.Title = $"{(ServerType) instance.Info.Type}:{instance.Info.Port}";

            ServerType = (ServerType) instance.Info.Type;
            
            await Server.ConfigureAsync(config);
        }
    }
}