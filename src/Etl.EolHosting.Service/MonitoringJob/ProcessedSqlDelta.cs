using System;

namespace Eol.Cig.Etl.EolHosting.Service.MonitoringJob
{
    internal class ProcessedSqlDelta
    {
        public string EnvironmentDelta { get; }
        public DateTime LatestProcessedDelta { get; }
        public DateTime EndExecutionTimeLatestProcessedDelta { get; }
        public bool FatalError { get; set; }

        public ProcessedSqlDelta(string environment, DateTime latestProcessedDelta, DateTime endExecutionTimeLatestProcessedDelta)
        {
            EnvironmentDelta = environment;
            LatestProcessedDelta = latestProcessedDelta;
            EndExecutionTimeLatestProcessedDelta = endExecutionTimeLatestProcessedDelta;
            FatalError = false;
        }

        public override string ToString()
        {
            return EnvironmentDelta + "," + LatestProcessedDelta + "," + EndExecutionTimeLatestProcessedDelta +","+FatalError;
        }
    }
}
