using Eol.Cig.Etl.EolHosting.Configuration;
using Eol.Cig.Etl.EolHosting.Extract;
using Eol.Cig.Etl.EolHosting.Load;
using Eol.Cig.Etl.EolHosting.Service.Configuration;
using Eol.Cig.Etl.Shared.Configuration;
using Eol.Cig.Etl.Shared.Service;
using Eol.Cig.Etl.Shared.Utils;
using StructureMap;
using System.Configuration;

namespace Eol.Cig.Etl.EolHosting.Service
{
    public static class Bootstrapper
    {
        private static readonly IEolHostingConfiguration Configuration = new EolHostingConfiguration(ConfigurationManager.AppSettings);
        private static readonly IJobConfigurationSection JobConfigSection = ConfigurationManager.GetSection("jobs") as JobConfigurationSection;
        private static readonly IJobConfiguration JobConfiguration = new JobConfiguration(JobConfigSection.Instances[JobNames.EolHosting.ToString()],ConfigurationManager.ConnectionStrings);

        static readonly IAwsConfiguration awsConfig = new AwsConfiguration();

        public static Container CreateContainer()
        {

            var parentContainer = BootstrapperUtils.CreateDefaultContainer(Configuration);
            var childContainer = parentContainer.CreateChildContainer();

            childContainer.Configure(cfg =>
            {
                cfg.For<IAwsConfiguration>().Use(awsConfig).Singleton();
                cfg.For<IEtlService>().Use<EtlEolHostingService>().Singleton();
                cfg.For<IEolHostingConfiguration>().Use(Configuration).Singleton();
                cfg.For<IServiceConfiguration>().Use(Configuration).Singleton();
                cfg.For<IEolHostingExtractorFactory>().Use<EolHostingExtractorFactory>();
                cfg.For<IEolHostingUploaderFactory>().Use<EolHostingUploaderFactory>();
                cfg.For<IEtlEolHostingServiceApi>().Use<EtlEolHostingServiceApi>();
                cfg.For<IJobConfiguration>().Use(JobConfiguration).Singleton().Named(JobNames.EolHosting.ToString());
                cfg.For<IEolHostingExtractor>().Use<EolHostingExtractor>().Ctor<IJobConfiguration>().IsNamedInstance(JobNames.EolHosting.ToString()).Named(JobNames.EolHosting.ToString());
                cfg.For<IEolHostingUploader>().Use<EolHostingUploader>().Ctor<IJobConfiguration>().IsNamedInstance(JobNames.EolHosting.ToString()).Named(JobNames.EolHosting.ToString());
            });
            return (Container)childContainer;
        }
    }
}
