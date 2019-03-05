using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using NLog;

namespace DowntimeOPC
{
    public class KashiraClient
    {
        //
        static bool autoAccept = false;
        // Constant reconnect period (default value 10)
        const int ReconnectPerion = 10;
        // Variable endpointURL (opc server address)
        private string endpointURL;
        // Variable client run time
        private int clientRunTime = Timeout.Infinite;
        // Variable session reconnect handler
        private SessionReconnectHandler reconnectHandler;
        // Variable session
        private Session session;

        private static ExitCode exitCode;

        private Logger logger = LogManager.GetLogger("OPCuaKashira");

        // Constructor for KashiraClient
        public KashiraClient(string _endpointURL, bool _autoAccept)
        {
            // Set endpointURL
            endpointURL = _endpointURL;
            // Set autoAccept
            autoAccept = _autoAccept;
            // Set clientRunTime
            clientRunTime = Timeout.Infinite;
        }

        public void Run()
        {
            try
            {
                ConsoleKashiraClient().Wait();
            }
            catch (Exception ex)
            {
                logger.Error("ServiceResultException: " + ex.Message);
                Utils.Trace("ServiceResultException: " + ex.Message);
                Console.WriteLine("Exception: {0}", ex.Message);
                return;
            }

            ManualResetEvent quitEvent = new ManualResetEvent(false);
            try
            {
                Console.CancelKeyPress += (sender, eArgs) =>
                {
                    quitEvent.Set();
                    eArgs.Cancel = true;
                };
            }
            catch
            {
            }
            // wait for timeout Ctrl-C
            quitEvent.WaitOne(clientRunTime);

            // return error conditions
            if (session.KeepAliveStopped)
            {
                logger.Error("Error no keep alive");
                exitCode = ExitCode.ErrorNoKeepAlive;
                return;
            }
            exitCode = ExitCode.Ok;
        }

        public static ExitCode ExitCode { get => exitCode; }

        private async Task ConsoleKashiraClient()
        {
            logger.Info("Create app configuration");
            exitCode = ExitCode.ErrorCreateApplication;
            ApplicationInstance application = new ApplicationInstance
            {
                ApplicationName = "UA Kashira Client",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = "Opc.Ua.KashiraClient"
            };
            
            logger.Info("Load application configuration");
            ApplicationConfiguration config = await application.LoadApplicationConfiguration(false);

            bool haveAppCertificate = await application.CheckApplicationInstanceCertificate(false, 0);
            if(!haveAppCertificate)
            {
                logger.Error("Application instance certificate invalid!");
                throw new Exception("Application instance certificate invalid!");
            }

            if(haveAppCertificate)
            {
                config.ApplicationUri = Utils.GetApplicationUriFromCertificate(config.SecurityConfiguration.ApplicationCertificate.Certificate);
                if(config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
                {
                    autoAccept = true;
                }
                config.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(CertificateValidator_CertificateValidation);
                logger.Info("Check the application certificate");
            }
            else
            {
                logger.Error("WARN: missing application certificate, using unsecure connection.");
            }
            logger.Info("Discover endpoints");
            exitCode = ExitCode.ErrorDiscoverEndpoints;
            var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointURL, haveAppCertificate, 15_000);
            
            logger.Info("Create session with OPC server");
            exitCode = ExitCode.ErrorCreateSession;
            var endpointConfiguration = EndpointConfiguration.Create(config);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);
            session = await Session.Create(config, endpoint, false, "OPC UA Kashira Client", 60_000, new UserIdentity(new AnonymousIdentityToken()), null);
            session.KeepAlive += Client_KeepAlive;

            logger.Info("Browse the OPC UA server namespace");
            exitCode = ExitCode.ErrorBrowseNamespace;
            ReferenceDescriptionCollection references;
            Byte[] continuationPoint;

            references = session.FetchReferences(ObjectIds.ObjectsFolder);
            session.Browse(
                null,
                null,
                ObjectIds.ObjectsFolder,
                0u,
                BrowseDirection.Forward,
                ReferenceTypeIds.HierarchicalReferences,
                true,
                (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method,
                out continuationPoint,
                out references);
            
            Console.WriteLine("DisplayName, BrowseName, NodeClass");
            foreach(var rd in references)
            {
                Console.WriteLine("{0}, {1}, {2}", rd.DisplayName, rd.BrowseName, rd.NodeClass);
                ReferenceDescriptionCollection nextRefs;
                byte[] nextCp;
                session.Browse(
                    null,
                    null,
                    ExpandedNodeId.ToNodeId(rd.NodeId, session.NamespaceUris),
                    0u,
                    BrowseDirection.Forward,
                    ReferenceTypeIds.HierarchicalReferences,
                    true,
                    (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method,
                    out nextCp,
                    out nextRefs);

                foreach (var nextRd in nextRefs)
                {
                    Console.WriteLine(" + {0}, {1}, {2}", nextRd.DisplayName, nextRd.BrowseName, nextRd.NodeClass);
                }
            }

            logger.Info("Create subscription with publishing interval of 1 sec");
            exitCode = ExitCode.ErrorCreateSubscription;
            var subscription = new Subscription(session.DefaultSubscription) { PublishingInterval = 1_000 };
            logger.Info("Add list of items (server current time and status) to the subscription");
            exitCode = ExitCode.ErrorMonitoredItem;
            var list = new List<MonitoredItem>{
                new MonitoredItem(subscription.DefaultItem)
                {
                    DisplayName = "PackingL1State", StartNodeId = "ns=3;s=V:0.3.104.1.0"
                },
                new MonitoredItem(subscription.DefaultItem)
                {
                    DisplayName = "PackingL3State", StartNodeId = "ns=3;s=V:0.3.104.1.1"
                },
                new MonitoredItem(subscription.DefaultItem)
                {
                    DisplayName = "HourChange", StartNodeId = "ns=3;s=V:0.3.104.1.2"
                }
            };
            list.ForEach(i => i.Notification += OnNotification);
            subscription.AddItems(list);

            logger.Info("Add the subscription to the session");
            exitCode = ExitCode.ErrorAddSubscription;
            session.AddSubscription(subscription);
            subscription.Create();

            Console.WriteLine("8 - Running...Press Ctrl-C to exit ...");
            exitCode = ExitCode.ErrorRunning;
        }

        // Helpers
        private void Client_KeepAlive(Session sender, KeepAliveEventArgs e)
        {
            if(e.Status != null && ServiceResult.IsNotGood(e.Status))
            {
                Console.WriteLine("{0} {1}/{2}", e.Status, sender.OutstandingRequestCount, sender.DefunctRequestCount);

                if(reconnectHandler == null)
                {
                    Console.WriteLine("--- RECONNECTING ---");
                    reconnectHandler = new SessionReconnectHandler();
                    reconnectHandler.BeginReconnect(sender, ReconnectPerion * 1000, Client_ReconnectComplete);
                }
            }
        }

        private void Client_ReconnectComplete(object sender, EventArgs e)
        {
            // ignore callback from discarded objects.
            if(!Object.ReferenceEquals(sender, reconnectHandler))
            {
                return;
            }

            session = reconnectHandler.Session;
            reconnectHandler.Dispose();
            reconnectHandler = null;

            logger.Error("RECONNECTED");
            logger.Info("RECONNECTED");
        }

        private static void OnNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
        {
            Logger logger = LogManager.GetLogger("OPCuaKashira");
            foreach (var value in item.DequeueValues())
            {
                logger.Info(item.DisplayName + ":" + value.Value + ", " + value.SourceTimestamp + ", " + value.StatusCode);
            }
        }

        private static void CertificateValidator_CertificateValidation(CertificateValidator validator, CertificateValidationEventArgs e)
        {
            if(e.Error.StatusCode == StatusCodes.BadCertificateUntrusted)
            {
                e.Accept = autoAccept;
                if(autoAccept)
                {
                    Console.WriteLine("Accepted Certificate: {0}", e.Certificate.Subject);
                }
                else
                {
                    Console.WriteLine("Rejected Certificate: {0}", e.Certificate.Subject);
                }
            }
        }
    }  
}