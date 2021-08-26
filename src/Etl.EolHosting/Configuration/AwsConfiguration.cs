using System;
using System.Configuration;

namespace Eol.Cig.Etl.EolHosting.Configuration
{
    public class AwsConfiguration : ConfigurationSection, IAwsConfiguration
    {
        public AwsConfiguration()
        {
            this.AwsSecretAccessKey = ConfigurationManager.AppSettings["AwsSecretAccessKey"];
            this.AwsAccessKeyId = ConfigurationManager.AppSettings["AwsAccessKeyId"];
            this.AwsKinesisStreamName = ConfigurationManager.AppSettings["AwsKinesisStreamName"];
            this.S3Prefix = ConfigurationManager.AppSettings["S3Prefix"];
            this.IsStreamingEnabled = Convert.ToBoolean(ConfigurationManager.AppSettings["IsStreamingEnabled"]);
        }

        public string AwsAccessKeyId { get; set; }
        public string AwsSecretAccessKey { get; set; }
        public string AwsKinesisStreamName { get; set; }
        public string S3Prefix { get; set; }
        public bool IsStreamingEnabled { get; set; }
    }
}
