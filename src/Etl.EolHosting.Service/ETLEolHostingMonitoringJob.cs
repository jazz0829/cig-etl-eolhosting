using Eol.Cig.Etl.EolHosting.Configuration;
using Eol.Cig.Etl.EolHosting.Service.MonitoringJob;
using log4net;
using Quartz;

namespace Eol.Cig.Etl.EolHosting.Service
{
    [DisallowConcurrentExecution]
    internal class EtlEolHostingMonitoringJob : IJob
    {
        private readonly IJobConfiguration _configuration;
        private readonly ILog _logger;
        public EtlEolHostingMonitoringJob(ILog logger, IJobConfiguration configuration) {
            _logger = logger;
            _configuration = configuration;
        }
        public void Execute(IJobExecutionContext context)
        {
            _logger.Info(@"EolHosting monitor started ");
            var eolHostingMonitor = new EolHostingMonitor(_logger, _configuration);
            eolHostingMonitor.CheckDailyDeltas();
            _logger.Info(@"EolHosting monitor stopped");
        }
    }
}
