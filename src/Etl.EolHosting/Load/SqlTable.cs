using System.Collections.Generic;

namespace Eol.Cig.Etl.EolHosting.Load
{
    public class SqlTable
    {
        public string Name { get; set; }
        public string Catalog { get; set;}
        public string Schema { get; set; }
        public Dictionary<string, string> Columns { get; set; }

        public SqlTable(string tableCatalog, string tableSchema, string tableName)
        {
            Catalog = tableCatalog;
            Schema = tableSchema;
            Name = tableName;
            Columns = new Dictionary<string, string>();
        }
    }
}