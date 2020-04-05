using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using Autofac;

[assembly: log4net.Config.XmlConfigurator]

namespace AmqpConsumerService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            #region Autofac

            var builder = new ContainerBuilder();

            builder.RegisterModule(new LoggingModule());
            builder.RegisterType<Receiver>();
            builder.RegisterType<Service1>();

            var container = builder.Build();

            #endregion Autofac

            var service = container.Resolve<Service1>();
#if DEBUG
            service.Start();
            service.Stop();
#else
            System.ServiceProcess.ServiceBase.Run(service);
#endif
        }
    }
}
