using System;
using Xunit;
using System.Collections.Generic;
using System.Linq;
using Eol.Cig.Etl.EolHosting.Domain;
using Moq;
using log4net;
using Eol.Cig.Etl.EolHosting.Configuration;

namespace Eol.CIg.Etl.EOLHosting.Test
{
    public class EolHostingTest
    {

        private Mock<ILog> _mockLog;
        private Mock<IJobConfiguration> _mockConfiguration;

        public EolHostingTest()
        {
            _mockLog = new Mock<ILog>();
            _mockConfiguration = new Mock<IJobConfiguration>();
        }

        [Fact]
        public void DateTimeSyntax()
        {
            var myDate = DateTime.ParseExact("20100101000001", "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
            Console.WriteLine(myDate);

        }

        [Fact]
        public void SortList()
        {

            var files = new List<CigFile>
            {
                new CigFile("CIG_Backup_NL_20160808221300.bak"),
                new CigFile("CIG_Backup_NL_20160807232007.bak"),
                new CigFile("CIG_Backup_NL_20160804142646.bak"),
                new CigFile("CIG_Backup_DE_20160808221300.bak"),
                new CigFile("CIG_Backup_DE_20160807232007.bak"),
                new CigFile("CIG_Backup_GB_20160804142646.bak")
            };


            var sortedFiles = files.OrderBy(o => o.Environment).ThenBy(o => o.DateTime).ToList();
            Assert.Equal("DE", sortedFiles[0].Environment);
            Assert.Equal(sortedFiles[0].DateTime, DateTime.ParseExact("20160807232007", "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}