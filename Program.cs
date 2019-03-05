using System;
using NLog;
using NLog.Targets;
using NLog.Config;

namespace DowntimeOPC
{
    class Program
    {
        public static int Main(string[] args)
        {

            bool autoAccept = true;
            string endpointURL = "opc.tcp://mos21-ibapda01:4840";

            var config = new LoggingConfiguration();

            var consoleTarget = new ColoredConsoleTarget("consoleTarget")
            {
                Layout = @"${date:format=HH\:mm\:ss} ${message} ${exception}"
            };
            config.AddTarget(consoleTarget);

            var fileTarget = new FileTarget("errorTarget")
            {
                FileName="${basedir}/logs/error.txt",
                Layout="${longdate} - ${level}: ${message} ${exception}"
            };
            config.AddTarget(fileTarget);

            var fileEventTarget = new FileTarget("fileTarget")
            {
                FileName="${basedir}/logs/event.txt",
                Layout="${longdate} - ${level}: ${message}"
            };

            config.AddRuleForOneLevel(LogLevel.Error, fileTarget);
            config.AddRuleForOneLevel(LogLevel.Info, fileEventTarget);
            config.AddRuleForAllLevels(consoleTarget);
            LogManager.Configuration = config;

            Logger logger = LogManager.GetLogger("OPCuaKashira");
            logger.Info("Start OPC UA Kashira Clients");
            KashiraClient client = new KashiraClient(_endpointURL: endpointURL, _autoAccept: autoAccept);
            client.Run();
            return (int)KashiraClient.ExitCode; 
        }
    }
}
