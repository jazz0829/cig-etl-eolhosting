using Eol.Cig.Etl.EolHosting.Service.Configuration;
using Eol.Cig.Etl.Shared.Service;
using log4net;
using Quartz;
using Quartz.Impl;
using System;
using System.IO;

namespace Eol.Cig.Etl.EolHosting.Service
{
    public class EtlEolHostingServiceApi : EtlServiceApi, IEtlEolHostingServiceApi
    {
        private readonly ILog _logger;
        public EtlEolHostingServiceApi(IEolHostingConfiguration configuration, ILog logger) : base(configuration)
        {
            _logger = logger;
        }

        public override string Execute()
        {
            var scheduler = StdSchedulerFactory.GetDefaultScheduler();
            var job = JobBuilder.Create<EtlEolHostingJob>().WithIdentity(JobNames.EolHosting.ToString(), "API").Build();
            var trigger = TriggerBuilder.Create()
                .WithIdentity(JobNames.EolHosting.ToString(), "API")
                .WithDescription("On demand execution: start now")
                .StartNow()
                .Build();
            scheduler.ScheduleJob(job, trigger);

            _logger.InfoFormat("On demand execution");
            return "Job started";
        }


        public string ExecuteSingleDelta(string path)
        {
            /*
             http://localhost/CustomerIntelligence/Etl/EolHosting/ExecuteSingleDelta?deltaFullPathToImport=\\nl000FS01\
            Custumer_Intelligence_Group\EOL_Hosting_Dev\CIG_Backup_ES_20161017005501.bak?overrideDateCheck=false
            */

            _logger.Info($"Requested restore of a single delta. Full path: {path}");
            //This supports having local paths in qoutes in the query parameter.
            var deltaFullPath = path.TrimStart('"').TrimEnd('"');

            if (File.Exists(deltaFullPath))
            {
                var jobName = JobNames.EolHosting.ToString();
                var scheduler = StdSchedulerFactory.GetDefaultScheduler();
                var job = scheduler.GetJobDetail(new JobKey(jobName));
                var jobDataMap = new JobDataMap { { "deltaFullPathToImport", deltaFullPath } };

                var trigger = TriggerBuilder.Create()
                    .WithIdentity($"OnDemand_{jobName}")
                    .ForJob(job)
                    .UsingJobData(jobDataMap)
                    .WithDescription("On demand execution: start now")
                    .StartNow()
                    .Build();

                try
                {
                    scheduler.ScheduleJob(trigger);
                    _logger.Info($"Executed {jobName} on demand.");

                }
                catch (Exception ex)
                {
                    _logger.Error($"Error occured during on deman execution of job: {jobName}.", ex);
                }
                finally
                {
                    scheduler.UnscheduleJob(trigger.Key);
                }

                _logger.InfoFormat($"On demand execution. Input delta file: {deltaFullPath}");
            }
            else
            {
                _logger.Error($"On demand execution. ExecuteSingleDelta: the input parameter is not correct {deltaFullPath}");
            }

            return "Job started";
        }

        public string DeleteSingleDelta(string deltaFullPath)
        {
            //http://localhost/CustomerIntelligence/Etl/EolHosting/DeleteSingleDelta?deltaFullPathToImport=\\nl000FS01\Custumer_Intelligence_Group\EOL_Hosting_Dev\CIG_Backup_ES_20161017005501.bak
            if (File.Exists(deltaFullPath))
            {
                var scheduler = StdSchedulerFactory.GetDefaultScheduler();
                var job = JobBuilder.Create<EtlEolHostingJob>().WithIdentity(JobNames.EolHosting.ToString(), "API").Build();
                job.JobDataMap["deltaFullPathToDelete"] = deltaFullPath;

                var trigger = TriggerBuilder.Create()
                    .WithIdentity(JobNames.EolHosting.ToString(), "API")
                    .WithDescription("On demand execution: start now")
                    .StartNow()
                    .Build();
                scheduler.ScheduleJob(job, trigger);
                _logger.InfoFormat($"On demand execution. Input delta file: {deltaFullPath}");
            }
            else
            {
                _logger.Error($"On demand execution. DeleteSingleDelta: Input delta file: {deltaFullPath}");
            }
            return "Job started";
        }
    }
}
