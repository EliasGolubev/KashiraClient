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