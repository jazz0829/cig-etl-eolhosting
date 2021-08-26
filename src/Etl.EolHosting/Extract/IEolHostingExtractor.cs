using Eol.Cig.Etl.EolHosting.Domain;
using System.Collections.Generic;

namespace Eol.Cig.Etl.EolHosting.Extract
{
    public interface IEolHostingExtractor
    {
        List<CigFile> GetLatestCigFiles();
        CigFile MoveFileFromSourceToStaging(CigFile cigFileSingleDelta);
        bool CheckLatestDeltas();
    }
}
