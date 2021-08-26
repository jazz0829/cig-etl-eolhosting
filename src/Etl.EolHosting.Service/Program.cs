using Eol.Cig.Etl.EolHosting.Service.Configuration;
using Eol.Cig.Etl.Shared.Extensions;
using Eol.Cig.Etl.Shared.Service;
using Quartz;
using Topshelf;
using Topshelf.Quartz.StructureMap;
using Topshelf.StructureMap;

namespace Eol.Cig.Etl.EolHosting.Service
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var host = HostFactory.New(c =>
            {
                var container = Bootstrapper.CreateContainer();
                c.UseStructureMap(container);
                var configuration = container.GetInstance<IEolHostingConfiguration>();
                c.ConfigureHost(configuration);
                c.Service<IEtlService>(s =>
                {
                    s.ConstructUsingStructureMap();
                    s.WhenStarted(service => service.Start());
                    s.WhenStopped(service => service.Stop());
                    s.UseQuartzStructureMap();
                    s.ScheduleQuartzJob(q =>
                        q.WithJob(() =>
                            JobBuilder.Create<EtlEolHostingJob>().WithIdentity(JobNames.EolHosting.ToString()).Build())
                            .AddTrigger(() =>
                                TriggerBuilder.Create()
                                    .WithDescription("Scheduled execution")
                                    .WithCronSchedule(configuration.Schedule)
                                    .Build())
                        );
                    s.ScheduleQuartzJob(q =>
                      q.WithJob(() =>
                          JobBuilder.Create<EtlEolHostingMonitoringJob>().Build())
                          .AddTrigger(() =>
                                             TriggerBuilder.Create()
                                                 .WithDescription("Scheduled execution")
                                                 .WithCronSchedule(configuration.ScheduleMonitoring)
                                                 .Build()));

                });

            });
            host.Run();
        }
    }
}