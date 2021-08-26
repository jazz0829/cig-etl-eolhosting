using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;
using Eol.Cig.Etl.Shared.Utils;
using Eol.Cig.Etl.EolHosting.Configuration;

namespace Eol.Cig.Etl.EolHosting.Service.MonitoringJob
{
    class EolHostingMonitor
    {
        private readonly IJobConfiguration _configuration;
        private readonly ILog _logger;

        public EolHostingMonitor(ILog logger, IJobConfiguration configuration)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public void CheckDailyDeltas()
        {
            var processedSqlDeltas = GetSqlProcessedDeltas();

            var today = DateTime.Now.Date;
            foreach (var processedSqlDelta in processedSqlDeltas)
            {
                if (processedSqlDelta.LatestProcessedDelta.Date < today)
                {
                    processedSqlDelta.FatalError = true;
                }
            }

            if (processedSqlDeltas.Any(d => d.FatalError))
            {
                _logger.Fatal(PrintResults(processedSqlDeltas, true));

            }
            else
            {
                _logger.InfoFormat(PrintResults(processedSqlDeltas));
            }
        }

        private static string PrintResults(IEnumerable<ProcessedSqlDelta> processedSqlDeltas, bool onlyFailed = false)
        {
            var sb = new StringBuilder();
            sb.Append("EnvironmentDelta, LatestProcessedDelta, EndExecutionTimeLatestProcessedDelta, FatalError");
            sb.Append(Environment.NewLine);
            foreach (var processedSqlDelta in processedSqlDeltas)
            {
                if (processedSqlDelta.FatalError && onlyFailed)
                {
                    sb.Append(processedSqlDelta);
                    sb.Append(Environment.NewLine);
                }
            }
            return sb.ToString();
        }

        private List<ProcessedSqlDelta> GetSqlProcessedDeltas()
        {
            var processedSqlDeltas = new List<ProcessedSqlDelta>();

            var sb = new StringBuilder();
            sb.Append("SELECT LatestBackup.Environment, LatestBackup.LatestBackupDateTime, BackupRestoreSummary.RestoredDateTime");
            sb.Append(" FROM ");
            sb.Append(" (SELECT Environment, max(BackupDateTime) AS LatestBackupDateTime FROM[config].[EOLHosting_BackupRestore_Summary] GROUP BY Environment) AS LatestBackup ");
            sb.Append(" JOIN ");
            sb.Append(" [config].[EOLHosting_BackupRestore_Summary] AS BackupRestoreSummary ");
            sb.Append(" ON LatestBackup.Environment = BackupRestoreSummary.Environment AND LatestBackup.latestBackupDateTime = BackupRestoreSummary.BackupDateTime ");
            sb.Append(" WHERE LatestBackup.Environment NOT IN ('US')");
            sb.Append(" ORDER BY LatestBackup.Environment ");

            var processedDeltasQuery = sb.ToString();
            using (var reader = SqlServerUtils.ExecuteCommandReturnReader(processedDeltasQuery, _configuration.DestinationConnectionString))
            {
                if (reader != null && reader.HasRows)
                {
                    while (reader.Read())
                    {
                        var environment = (string)reader.GetValue(0);
                        var latestProcessedDelta = (DateTime)reader.GetValue(1);
                        var endExecutionTimeLatestProcessedDelta = (DateTime)reader.GetValue(2);
                        processedSqlDeltas.Add(new ProcessedSqlDelta(environment, latestProcessedDelta, endExecutionTimeLatestProcessedDelta));
                    }
                }
            }
            return processedSqlDeltas;
        }
    }
}
