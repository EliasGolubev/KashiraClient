using System;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DowntimeOPC
{
    class Program
    {
        public static int Main(string[] args)
        {
            bool autoAccept = true;
            string endpointURL = "opc.tcp://mos21-ibapda01:4840";

            KashiraClient client = new KashiraClient(_endpointURL: endpointURL, _autoAccept: autoAccept);
            client.Run();
            return (int)KashiraClient.ExitCode; 
        }
    }
}
