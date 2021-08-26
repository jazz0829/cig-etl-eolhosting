using System.Collections.Generic;
using System.Text;

namespace Eol.Cig.Etl.EolHosting.Load
{
    public class SchemaChanges
    {
        public Dictionary<string, List<KeyValuePair<string, string>>> Changes { get; set; }
        public SchemaChanges()
        {
            this.Changes = new Dictionary<string, List<KeyValuePair<string, string>>>();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var item in Changes)
            {
                sb.AppendLine($"Table: {item.Key}");
                foreach (KeyValuePair<string, string> pair in item.Value)
                {
                    sb.AppendLine($"Column: {pair.Key} \t DataType: {pair.Value}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
