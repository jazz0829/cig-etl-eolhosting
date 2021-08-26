using Eol.Cig.Etl.Shared.Configuration;
using Eol.Cig.Etl.Shared.Extensions;
using System.Collections.Specialized;

namespace Eol.Cig.Etl.EolHosting.Service.Configuration
{
    public class EolHostingConfiguration : ServiceConfiguration, IEolHostingConfiguration
    {
        public EolHostingConfiguration(NameValueCollection appSettings) : base(appSettings)
        {
            Schedule = appSettings.GetStringOrThrow("SCHEDULE");
            ScheduleMonitoring = appSettings.GetStringOrThrow("SCHEDULE-MONITORING");
        }

        public string Schedule { get; }
        public string ScheduleMonitoring { get; }
    }
}
