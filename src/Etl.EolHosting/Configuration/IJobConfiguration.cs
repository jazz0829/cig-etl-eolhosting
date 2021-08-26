using Eol.Cig.Etl.Shared.Configuration;

namespace Eol.Cig.Etl.EolHosting.Configuration
{
    public interface IJobConfiguration : ISqlJobConfiguration
    {
        string SourceFolder { get; }
        string StagingFolder { get; }
        string TempFolder { get; }
        string SummaryTableName { get; }
        string SourceConnectionStringName { get; }
        string SourceConnectionString { get; }
    }
}
