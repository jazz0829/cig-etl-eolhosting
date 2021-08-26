using Eol.Cig.Etl.EolHosting.Configuration;
using Eol.Cig.Etl.EolHosting.Extract;
using Eol.Cig.Etl.EolHosting.Load;
using Eol.Cig.Etl.Shared.Integration;
using Eol.Cig.Etl.Shared.Utils;
using log4net;
using StructureMap;
using System.Configuration;

namespace Eol.Cig.Etl.EolHosting.Integration
{
    public static class Bootstrapper
    {
        public static readonly string JobName = "EolHosting";
        static readonly IJobConfigurationSection JobConfigSection = ConfigurationManager.GetSection("jobs") as JobConfigurationSection;


        private static readonly string[] DefaultStrings = { "Destination_Default", "Source_Default" };
        private static readonly string[] RandomStrings = { "Destination", "Source" };

        static readonly ConnectionStringSettingsCollection RandomConnectionStringSettings = GetRandomConnectionStringSettings(DefaultStrings, RandomStrings);

        static readonly IJobConfiguration JobConfiguration = new JobConfiguration(JobConfigSection.Instances[JobName], RandomConnectionStringSettings);
        static readonly IAwsConfiguration awsConfig = new AwsConfiguration() {IsStreamingEnabled = false };
        public static Container CreateContainer()
        {
            return new Container(cfg =>
            {
                cfg.For<ILog>().Use(x => LogManager.GetLogger(x.RequestedName));
                cfg.For<IEolHostingExtractorFactory>().Use<EolHostingExtractorFactory>();
                cfg.For<IEolHostingUploaderFactory>().Use<EolHostingUploaderFactory>();
                cfg.For<IAwsConfiguration>().Use(awsConfig).Singleton();
                cfg.For<IJobConfiguration>()
                        .Use(JobConfiguration)
                        .Singleton()
                        .Named(JobName);
                cfg.For<IEolHostingExtractor>()
                        .Use<EolHostingExtractor>()
                        .Ctor<IJobConfiguration>()
                        .IsNamedInstance(JobName)
                        .Named(JobName);
                cfg.For<IEolHostingUploader>()
                        .Use<EolHostingUploader>()
                        .Ctor<IJobConfiguration>()
                        .IsNamedInstance(JobName)
                        .Named(JobName);
            });
        }



        private static ConnectionStringSettingsCollection GetRandomConnectionStringSettings(string[] defaultConnectionStringName, string[] randomConnectionStringName)
        {
            var connectionStringSettings = ConnectionStringHelper.GetConnectionStringSettingWithRandomInitialCatalog(defaultConnectionStringName[0], randomConnectionStringName[0]);

            var defaultConnectionString = ConnectionStringHelper.GetDefaultConnectionString(defaultConnectionStringName[1]);
            var masterDbConnectionString = SqlServerUtils.ChangeInitialCatalogConnectionString("master", defaultConnectionString);
            connectionStringSettings.Add(new ConnectionStringSettings(randomConnectionStringName[1], masterDbConnectionString));

            return connectionStringSettings;
        }
    }
}
