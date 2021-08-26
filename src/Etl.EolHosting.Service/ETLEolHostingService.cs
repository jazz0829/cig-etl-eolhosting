using Eol.Cig.Etl.EolHosting.Service.Configuration;
using Eol.Cig.Etl.Shared.Api;
using Eol.Cig.Etl.Shared.Service;
using log4net;

namespace Eol.Cig.Etl.EolHosting.Service
{
    public class EtlEolHostingService : EtlService<IEtlEolHostingServiceApi, EtlEolHostingServiceApi>
    {

        private readonly ILog _logger;
        private readonly IEolHostingConfiguration _configuration;

        public EtlEolHostingService(IEolHostingConfiguration configuration, ILog logger, IHostedApiFactory hostedApiFactory) : base(configuration, logger, hostedApiFactory)
        {
            _logger = logger;
            _configuration = configuration;
        }

        protected override void LogSchedule()
        {
            _logger.InfoFormat("Job {0} Starts at {1}", JobNames.EolHosting.ToString(), _configuration.Schedule);
        }

    }
}
