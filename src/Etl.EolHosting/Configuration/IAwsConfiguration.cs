namespace Eol.Cig.Etl.EolHosting.Configuration
{
    public interface IAwsConfiguration
    {
        string AwsAccessKeyId { get; }
        string AwsSecretAccessKey { get; }
        string AwsKinesisStreamName { get; }
        string S3Prefix { get; }
        bool IsStreamingEnabled { get; }
    }
}
