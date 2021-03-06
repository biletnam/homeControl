﻿using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using homeControl.Domain.Events.Configuration;
using homeControl.Interop.Rabbit.IoC;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using Serilog;
using Serilog.Events;
using StructureMap;

namespace homeControl.ConfigurationStore
{
    internal sealed class ConfigurationStoreEntry
    {
        private static readonly ILogger _log;
        private static readonly IConfigurationRoot _config =
            new ConfigurationBuilder()
                .AddJsonFile("settings.json")
                .Build();

        static ConfigurationStoreEntry()
        {
            var level = (LogEventLevel)Enum.Parse(typeof(LogEventLevel), _config["LogEventLevel"]);

            _log = new LoggerConfiguration()
                .MinimumLevel.Is(level)
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} (from {SourceContext}){NewLine}{Exception}")
                .CreateLogger()
                .ForContext(typeof(ConfigurationStoreEntry));
            _log.Debug("Logging initialized.");
        }

        private static readonly IContainer _rootContainer = BuildContainer();
        private static IContainer BuildContainer()
        {
            var container = new Container(cfg =>
            {
                var configPath = Path.Combine(Directory.GetCurrentDirectory(), "conf");
                cfg.ForConcreteType<ConfigurationProvider>()
                   .Configure
                   .Ctor<string>("configurationsDirectory").Is(configPath);
                cfg.ForConcreteType<ConfigurationRequestsProcessor>();

                cfg.AddRegistry(new RabbitConfigurationRegistryBuilder(_config)
                    .UseJsonSerializationWithEncoding(Encoding.UTF8)
                    .SetupEventSource<ConfigurationRequestEvent>("configuration-requests", ExchangeType.Fanout, string.Empty)
                    .SetupEventSender<ConfigurationResponseEvent>("configuration")
                    .Build());

                cfg.For<ILogger>().Use(c => _log.ForContext(c.ParentType));
            });

            return container;
        }

        static void Main(string[] args)
        {
            var asmName = Assembly.GetEntryAssembly().GetName();
            Console.Title = $"{asmName.Name} v.{asmName.Version.ToString(3)}";

            _log.Information($"Starting service: {Console.Title}");

            using (var workContainer = _rootContainer.GetNestedContainer())
            using (var cts = new CancellationTokenSource())
            {
                workContainer.Configure(c => c.For<CancellationToken>().Use(() => cts.Token));

                Console.CancelKeyPress += (s, e) => cts.Cancel();

                Run(workContainer, cts.Token);
            }
        }

        private static void Run(IContainer workContainer, CancellationToken ct)
        {
            workContainer.GetInstance<ConfigurationRequestsProcessor>().Run(ct).Wait(ct);
        }
    }
}