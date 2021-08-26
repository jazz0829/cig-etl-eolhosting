using Eol.Cig.Etl.Shared.Configuration;

namespace Eol.Cig.Etl.EolHosting.Service.Configuration
{
    public interface IEolHostingConfiguration : IServiceConfiguration
    {
        string Schedule { get; }
        string ScheduleMonitoring { get; }
    }
}
