using Eol.Cig.Etl.EolHosting.Configuration;
using Eol.Cig.Etl.EolHosting.Domain;
using Eol.Cig.Etl.Shared.Utils;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;

namespace Eol.Cig.Etl.EolHosting.Extract
{
    public class EolHostingExtractor : IEolHostingExtractor
    {
        private const string DeltaNameWellFormed = "CIG_Backup_[A-Za-z]{2}_[0-9]{14}.bak";
        private readonly ILog _logger;
        private readonly IJobConfiguration _configuration;

        public EolHostingExtractor(ILog logger, IJobConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public List<CigFile> GetLatestCigFiles()
        {
            var cigFiles = new List<CigFile>();

            if (Directory.Exists(_configuration.SourceFolder))
            {
                var files = Directory.GetFiles(_configuration.SourceFolder);

                foreach (var filePath in files)
                {
                    try
                    {
                        if (StringUtils.MatchPattern(filePath, DeltaNameWellFormed))
                        {
                            var cigFile = GetLaterCigFile(filePath);
                            if (cigFile != null)
                            {
                                cigFiles.Add(MoveFileFromSourceToStaging(cigFile));
                                _logger.Info($"Copied file {cigFile.FileName} from source folder {_configuration.SourceFolder} to staging folder {_configuration.StagingFolder}");
                            }
                        }
                    }
                    catch
                    {
                        _logger.Error($"Error occured when copying {filePath} files from {_configuration.SourceFolder} to {_configuration.StagingFolder}");
                    }
                }
            }
            else
            {
                _logger.Error($"The sourceFolder {_configuration.SourceFolder} does not exist.");
            }
            return cigFiles;
        }

        private CigFile GetLaterCigFile(string filePath)
        {
            var cigFile = new CigFile(filePath);
            FileUtils.WaitFileInUse(cigFile.FullPath);

            try
            {
                var query = $"SELECT MAX(BackupDateTime) FROM {_configuration.SummaryTableName} where Environment='{cigFile.Environment}'";
                using (var reader = SqlServerUtils.ExecuteCommandReturnReader(query, _configuration.DestinationConnectionString))
                {
                    if (reader != null && reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            if (reader.IsDBNull(0) || cigFile.DateTime > (DateTime)reader.GetValue(0))
                            {
                                return cigFile;
                            }
                        }
                    }
                    else
                    {
                        return cigFile;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error when reading from table {_configuration.SummaryTableName}" , ex);
                throw;
            }
            return null;
        }

        public CigFile MoveFileFromSourceToStaging(CigFile cigFileSingleDelta)
        {
            Retry.Do(() => FileUtils.CopyFile(cigFileSingleDelta.FilePath, cigFileSingleDelta.FileName, _configuration.StagingFolder), TimeSpan.FromSeconds(60), 5);
            return new CigFile(_configuration.StagingFolder + "\\" + cigFileSingleDelta.FileName);
        }

        public bool CheckLatestDeltas()
        {
            var completeListDeltas = false;
            var deltaEnvironments = GetDeltasEnvironments();

            if (Directory.Exists(_configuration.SourceFolder))
            {
                var files = Directory.GetFiles(_configuration.SourceFolder);
                var today = DateTime.Now.ToString("yyyyMMdd");

                foreach (var environment in deltaEnvironments)
                {
                    var latestDelta = false;
                    foreach (var filePath in files)
                    {
                        if (filePath.Contains(environment) && filePath.Contains(today))
                        {
                            latestDelta = true;
                            completeListDeltas = true;
                        }
                    }
                    if (!latestDelta)
                    {
                        _logger.Error($"{_configuration.SourceFolder} does not contain the delta for {environment} for {today}");
                    }
                }
            }
            return completeListDeltas;
        }

        private List<string> GetDeltasEnvironments()
        {
            var deltaEnvironments = new List<string>();
            try
            {
                var query = $"select Environment FROM {_configuration.SummaryTableName} GROUP BY Environment";
                using (var reader = SqlServerUtils.ExecuteCommandReturnReader(query, _configuration.DestinationConnectionString))
                {
                    if (reader != null && reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            deltaEnvironments.Add(((string)reader.GetValue(0)).Trim());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error when reading from table {_configuration.SummaryTableName}", ex);
                throw;
            }
            return deltaEnvironments;
        }
    }
}
