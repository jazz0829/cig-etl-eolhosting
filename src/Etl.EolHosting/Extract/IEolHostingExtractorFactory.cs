namespace Eol.Cig.Etl.EolHosting.Extract
{
    public interface IEolHostingExtractorFactory
    {
        IEolHostingExtractor Create(string jobName);
    }
}