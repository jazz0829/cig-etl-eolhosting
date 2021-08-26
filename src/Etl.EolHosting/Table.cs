using System.Collections.Generic;

namespace Eol.Cig.Etl.EolHosting
{
    public class Table
    {
        public string Name { get; set; }
        public List<string> Columns { get; set; }
        public bool? AutoAddColumns { get; set; }
        public bool? StreamingEnabled { get; set; }
    }
}
