using Eol.Cig.Etl.EolHosting.Domain;
using Eol.Cig.Etl.EolHosting.Extract;
using Eol.Cig.Etl.EolHosting.Load;
using Eol.Cig.Etl.Shared.Service;
using log4net;
using Quartz;
using System;
using System.Linq;

namespace Eol.Cig.Etl.EolHosting.Service
{
    [DisallowConcurrentExecution]
    public class EtlEolHostingJob : IJob
    {
        private readonly ILog _logger;
        private readonly IEolHostingExtractorFactory _eolHostingExtractorFactory;
        private readonly IEolHostingUploaderFactory _eolHostingUploaderFactory;
        private readonly IEtlService _etlService;

        public EtlEolHostingJob(ILog logger, IEolHostingExtractorFactory eolHostingExtracterFactory,
            IEolHostingUploaderFactory eolHostingUploaderFactory, IEtlService etlService)
        {
            _etlService = etlService;
            _logger = logger;
            _eolHostingExtractorFactory = eolHostingExtracterFactory;
            _eolHostingUploaderFactory = eolHostingUploaderFactory;
        }

        public void Execute(IJobExecutionContext context)
        {
            var jobName = context.JobDetail.Key.Name;
            var groupName = context.JobDetail.Key.Group;
            ThreadContext.Properties["JobName"] = jobName;
            ThreadContext.Properties["JobGroupName"] = groupName;

            _logger.InfoFormat("Job {0} from group {1} executing...", jobName, groupName);
            _logger.InfoFormat("Triggered by: {0}", context.Trigger.Description);
            try
            {
                var deltaFullPathToImport = context.MergedJobDataMap.GetString("deltaFullPathToImport");
                var deltaFullPathToDelete = context.MergedJobDataMap.GetString("deltaFullPathToDelete");

                if (!string.IsNullOrEmpty(deltaFullPathToImport))
                {
                    ImportSingleDelta(jobName, deltaFullPathToImport);
                }

                else if (!string.IsNullOrEmpty(deltaFullPathToDelete))
                {
                    DeleteSingleDelta(jobName, deltaFullPathToDelete);
                }
                else
                {
                    //DEFAULT

                    //Extract
                    var eolHostingExtracter = _eolHostingExtractorFactory.Create(jobName);
                    var files = eolHostingExtracter.GetLatestCigFiles();

                    //Load
                    if (files.Count > 0)
                    {
                        //Transform
                        files = files.OrderBy(o => o.Environment).ThenBy(o => o.DateTime).ToList();

                        var eolHostingUploader = _eolHostingUploaderFactory.Create(jobName);
                        foreach (var cigFile in files)
                        {
                            eolHostingUploader.HandleDelta(cigFile, false);
                        }
                    }
                    else
                    {
                        _logger.InfoFormat("There are no delta to process.");
                    }
                    _logger.InfoFormat("Job {0} from group {1} executed.", jobName, groupName);
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat("Job {0} from group {1} failed. Sending stop command...", jobName, groupName, ex);
                _etlService.ForcedStop();
                _logger.ErrorFormat("Job {0} from group {1} failed. Stop command sent", jobName, groupName, ex);
            }
        }

        private void DeleteSingleDelta(string jobName, string deltaFullPathToDelete)
        {
            try
            {
                var cigFileSingleDelta = new CigFile(deltaFullPathToDelete);

                var eolHostingExtractor = _eolHostingExtractorFactory.Create(jobName);
                cigFileSingleDelta = eolHostingExtractor.MoveFileFromSourceToStaging(cigFileSingleDelta);

                var eolHostingUploader = _eolHostingUploaderFactory.Create(jobName);
                eolHostingUploader.DeleteDelta(cigFileSingleDelta);
            }
            catch (Exception ex)
            {
                _logger.Error("Import of a single delta failed", ex);
                throw;
            }
        }

        private void ImportSingleDelta(string jobName, string deltaFullPathToImport)
        {
            try
            {
                var cigFileSingleDelta = new CigFile(deltaFullPathToImport);

                var eolHostingExtractor = _eolHostingExtractorFactory.Create(jobName);
                cigFileSingleDelta = eolHostingExtractor.MoveFileFromSourceToStaging(cigFileSingleDelta);

                var eolHostingUploader = _eolHostingUploaderFactory.Create(jobName);
                eolHostingUploader.DeleteDelta(cigFileSingleDelta);
                eolHostingUploader.HandleDelta(cigFileSingleDelta, true);
            }
            catch (Exception ex)
            {
                _logger.Error("Import of a single delta failed", ex);
                throw;
            }
        }
    }
}
