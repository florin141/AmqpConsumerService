using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Timers;
using Amqp;
using Amqp.Framing;
using log4net;

namespace AmqpConsumerService
{
    public class Receiver : IDisposable
    {
        // AMQP connection selection
        List<Address> addresses;
        int aIndex = 0;

        // Protocol objects
        Connection connection;
        Session session;
        ReceiverLink receiver;

        // Sender is ready to send messages 
        private readonly ManualResetEvent _connected = new ManualResetEvent(false);

        // Time in mS to wait for a connection to connect and become sendable
        // before failing over to the next host.
        const Int32 WAIT_TIME = 10 * 1000;

        // Application mission state
        private readonly ILog _log;

        /// <summary>
        /// Application constructor
        /// </summary>
        /// <param name="_addresses">Address objects that define the host, port, and target for messages.</param>
        /// <param name="_nToSend">Message count.</param>
        public Receiver(ILog log)
        {
            #region Adresses

            string addrs = "amqp://guest:guest@localhost:5672";

            addresses = new List<Address>();
            foreach (var adr in addrs.Split(',').ToList())
            {
                addresses.Add(new Address(adr));
            }

            #endregion

            _log = log;
        }

        /// <summary>
        /// Connection closed event handler
        /// 
        /// This function provides information only. Calling Reconnect is redundant with
        /// calls from the Run loop.
        /// </summary>
        /// <param name="_">Connection that closed. There is only one connection so this is ignored.</param>
        /// <param name="error">Error object associated with connection close.</param>
        private void ConnectionOnClosed(IAmqpObject sender, Error error)
        {
            if (error == null)
                _log.WarnFormat("Connection closed with no error");
            else
            {
                _log.ErrorFormat("Connection closed with error: {0}", error);

                Reconnect();
            }
        }

        private void SessionOnClosed(IAmqpObject sender, Error error)
        {
            if (error == null)
                _log.WarnFormat("Session closed with no error");
            else
            {
                _log.ErrorFormat("Session closed with error: {0}", error);

            }
        }

        private void ReceiverOnClosed(IAmqpObject sender, Error error)
        {
            if (error == null)
                _log.WarnFormat("Receiver closed with no error");
            else
            {
                _log.ErrorFormat("Receiver closed with error: {0}", error);
            }
        }

        /// <summary>
        /// Select the next host in the Address list and start it
        /// </summary>
        void Reconnect()
        {
            _log.InfoFormat("Entering Reconnect()");

            _connected.Reset();

            if (!_connected.WaitOne(WAIT_TIME))
            {
                OpenConnection();
            }
        }


        /// <summary>
        /// Start the current host in the address list
        /// </summary>
        async void OpenConnection()
        {
            try
            {
                _log.InfoFormat("Attempting connection to {0}:{1}", addresses[aIndex].Host, addresses[aIndex].Port);

                connection = await Connection.Factory.CreateAsync(addresses[aIndex], null, OnOpened);
                connection.Closed += ConnectionOnClosed;

                _log.InfoFormat("Success: connecting to {0}:{1}", addresses[aIndex].Host, addresses[aIndex].Port);
            }
            catch (Exception e)
            {
                _log.ErrorFormat("Failure: exception connecting to '{0}:{1}': {2}", addresses[aIndex].Host, addresses[aIndex].Port, e.Message);

                Reconnect();
            }
        }

        /// <summary>
        /// AMQP connection has opened. This callback may be called before
        /// ConnectAsync returns a value to the _connection_ variable.
        /// </summary>
        /// <param name="conn">Which connection. </param>
        /// <param name="__">Peer AMQP Open (ignored).</param>
        void OnOpened(IConnection conn, Open __)
        {
            _log.InfoFormat("Event: OnOpened");

            connection = (Connection)conn;

            _connected.Set();

            session = new Session(connection, new Begin(), OnBegin);
            session.Closed += SessionOnClosed;
        }



        /// <summary>
        /// AMQP session has opened
        /// </summary>
        /// <param name="_">Which session (ignored).</param>
        /// <param name="__">Peer AMQP Begin (ignored).</param>
        void OnBegin(ISession _, Begin __)
        {
            _log.InfoFormat("Event: OnBegin");

            if (receiver == null || receiver.IsClosed)
            {
                string targetName = addresses[aIndex].Path.Substring(1); // no leading '/'
                Source source = new Source() { Address = targetName };
                receiver = new ReceiverLink(session, "receiver-1", source, null);
                receiver.Start(1, OnMessage);
                receiver.Closed += ReceiverOnClosed;
            }
        }

        /// <summary>
        /// AMQP Link has attached. Signal that protocol stack is ready to receive.
        /// </summary>
        /// <param name="link"></param>
        /// <param name="attach"></param>
        //private void OnAttached(ILink link, Attach attach)
        //{
        //    _log.InfoFormat("Event: OnAttached");

        //    //((ReceiverLink) link).Start(200, OnMessage);
        //}

        private void OnMessage(IReceiverLink receiverLink, Message message)
        {
            _log.InfoFormat("Received {0}", message.Body);
            receiverLink.SetCredit(1, CreditMode.Drain);
        }

        /// <summary>
        /// Application mission code.
        /// Send N messages while automatically reconnecting to broker/peer as necessary.
        /// </summary>
        public void Start()
        {
            OpenConnection();
        }

        public void Dispose()
        {
            receiver.Close();
            session.Close();
            connection.Close();
        }
    }
}