using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;

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
                exitCode = ExitCode.ErrorNoKeepAlive;
                return;
            }
            exitCode = ExitCode.Ok;
        }

        public static ExitCode ExitCode { get => exitCode; }

        private async Task ConsoleKashiraClient()
        {
            /* STEP 1 - CREATE APP CONFIGURATION */
            exitCode = ExitCode.ErrorCreateApplication;
            ApplicationInstance application = new ApplicationInstance
            {
                ApplicationName = "UA Kashira Client",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = "Opc.Ua.KashiraClient"
            };

            /* STEP 2 - LOAD APPLICATION CONFIGURATION */
            ApplicationConfiguration config = await application.LoadApplicationConfiguration(false);

            /* STEP 3 - CHECK THE APPLICATION CERTIFICATE */
            bool haveAppCertificate = await application.CheckApplicationInstanceCertificate(false, 0);
            if(!haveAppCertificate)
            {
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
            }
            else
            {
                Console.WriteLine("WARN: missing application certificate, using unsecure connection.");
            }

            /* STEP 4 - DISCOVER ENDPOINTS*/
            exitCode = ExitCode.ErrorDiscoverEndpoints;
            var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointURL, haveAppCertificate, 15_000);

            /* STEP 5 - CREATE SESSION WITH OPC UA SERVER */
            exitCode = ExitCode.ErrorCreateSession;
            var endpointConfiguration = EndpointConfiguration.Create(config);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);
            session = await Session.Create(config, endpoint, false, "OPC UA Console Client", 60_000, new UserIdentity(new AnonymousIdentityToken()), null);
            session.KeepAlive += Client_KeepAlive;

            /* STEP 6 - BROWSE THE OPC UA SERVER NAMESPACE */
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

            Console.WriteLine("--- RECONNECTED ---");
        }

        private static void OnNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
        {
            foreach (var value in item.DequeueValues())
            {
                Console.WriteLine("{0}: {1}, {2}, {3}", item.DisplayName, value.Value, value.SourceTimestamp, value.StatusCode);
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