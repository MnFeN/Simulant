using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Simulant.Game.Data
{
    internal static class NpcIndexCsv
    {
        internal const string ActionHeader = "ActionId,BNpcBaseId";
        internal const string NameHeader = "BNpcBaseId,BNpcNameId";

        internal static IEnumerable<(uint First, uint Second)> ReadResource(string name, string header)
        {
            var stream = typeof(NpcIndexCsv).Assembly.GetManifestResourceStream("Simulant.Data." + name)
                ?? throw new FileNotFoundException("Missing embedded NPC index.", name);
            using (var reader = new StreamReader(stream))
            {
                foreach (var link in Read(reader, header))
                    yield return link;
            }
        }

        internal static IEnumerable<(uint First, uint Second)> Read(TextReader reader, string header)
        {
            if (reader.ReadLine() != header)
                throw new InvalidDataException($"Expected CSV header: {header}");

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0)
                    continue;

                var columns = line.Split(',');
                if (columns.Length != 2 ||
                    !uint.TryParse(columns[0], NumberStyles.None, CultureInfo.InvariantCulture, out var first) ||
                    !uint.TryParse(columns[1], NumberStyles.None, CultureInfo.InvariantCulture, out var second))
                    throw new InvalidDataException($"Invalid NPC index row: {line}");

                yield return (first, second);
            }
        }
    }
}
