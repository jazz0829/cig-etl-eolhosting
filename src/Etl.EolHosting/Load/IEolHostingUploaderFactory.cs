namespace Eol.Cig.Etl.EolHosting.Load
{
    public interface IEolHostingUploaderFactory
    {
        IEolHostingUploader Create(string jobName);
    }
}