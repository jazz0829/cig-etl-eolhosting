using System;

namespace Eol.Cig.Etl.EolHosting.Domain
{
    public class CigFile
    {
        private const string DateTimeFormat = "yyyyMMddHHmmss";

        public string FullPath { get; set; }
        public string Environment { get; set; }
        public DateTime DateTime { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string FileNameNoExtension { get; set; }
        public string RestoredDatabaseName { get; internal set; }

        public CigFile(string fullPath)
        {
            FullPath = fullPath;

            var splittedPath = FullPath.Split('\\');

            FileName = splittedPath[splittedPath.Length - 1];
            FileNameNoExtension = FileName.Substring(0, FileName.Length - 4);
            FilePath = fullPath.Replace(FileName, "");

            var splittedName = FileName.Substring(0, FileName.Length - 4).Split('_');

            Environment = splittedName[2];
            DateTime = DateTime.ParseExact(splittedName[3], DateTimeFormat, System.Globalization.CultureInfo.InvariantCulture);
        }


    }
}
