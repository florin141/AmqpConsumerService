using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace AmqpConsumerService
{
    public partial class Service1 : ServiceBase
    {
        private readonly Receiver _receiver;
        private readonly ILog _log;

        public Service1(Receiver receiver, ILog log)
        {
            _receiver = receiver;
            _log = log;

            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            _log.Info("In OnStart.");

            _receiver.Start();
        }

        protected override void OnStop()
        {
            _log.Info("In OnStop.");

            _receiver.Dispose();
        }

        internal void Start()
        {
            OnStart(null);

            Console.WriteLine("Service started. Press any key to shutdown the service.");
            Console.ReadLine();
        }
    }
}
