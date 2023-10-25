using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tk2Text
{
    internal class iStringReplacer
    {
        public readonly string FilePath;

        // 合字を想定し、置換を InvariantCulture で行う
        // 構文解析においては Ordinal

        public readonly Dictionary <string, string> Replacements = new Dictionary <string, string> (StringComparer.InvariantCulture);

        public iStringReplacer (string filePath)
        {
            FilePath = filePath;

            if (File.Exists (FilePath))
            {
                foreach (string xLine in File.ReadAllLines (FilePath, Encoding.UTF8))
                {
                    if (xLine.StartsWith ("//", StringComparison.Ordinal))
                        continue;

                    int xIndex = xLine.IndexOf ('|', StringComparison.Ordinal);

                    if (xIndex < 0)
                        continue;

                    string xKey = xLine.Substring (0, xIndex),
                        xValue = xLine.Substring (xIndex + 1);

                    if (xKey.Length == 0)
                        continue;

                    if (Replacements.ContainsKey (xKey))
                        Replacements [xKey] = xValue;

                    else Replacements.Add (xKey, xValue);
                }
            }
        }

        public string ReplaceAll (string value)
        {
            // 先頭から見ていき、そこに一つでもあるか探す実装を最初に考えたが、それでは置換しすぎたものを後続の置換で無理やり戻せない
            // この実装なら Dictionary である必要もないが、直すほど遅くないし、より良い実装を思い付く可能性もある

            foreach (KeyValuePair <string, string> xPair in Replacements)
                value = value.Replace (xPair.Key, xPair.Value, StringComparison.InvariantCulture);

            return value;
        }
    }
}
