using Eol.Cig.Etl.Shared.Load;
using Eol.Cig.Etl.EolHosting.Domain;

namespace Eol.Cig.Etl.EolHosting.Load
{
    public interface IEolHostingUploader : ISqlServerUploader
    {
        void HandleDelta(CigFile file, bool overwriteAppendedBackup);
        void DeleteDelta(CigFile cigFileSingleDelta);
    }
}
