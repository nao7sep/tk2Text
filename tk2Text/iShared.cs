using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Nekote;

namespace tk2Text
{
    internal static class iShared
    {
        public static readonly string AppDirectoryPath = nPath.GetDirectoryPath (Assembly.GetExecutingAssembly ().Location);

        public static string ToUnixDirectorySeparators (string path)
        {
            return path.Replace ('\\', '/');
        }

        public static string ToWindowsDirectorySeparators (string path)
        {
            return path.Replace ('/', '\\');
        }

        public static readonly string HalfWidthSpaceReplacementString = "&nbsp;";

        public static readonly string TabReplacementString = "&nbsp;&nbsp;&nbsp;&nbsp;";

        public static string ReplaceIndentationChars (string value)
        {
            // 今のところ半角空白とタブ文字だけに対応
            // それ以外を使うことが自分はない
            // 「どの文字なら何に置換」の List か何かを用意することも考えたが、速度も考えて、まずはシンプルに

            if (string.IsNullOrEmpty (value) == false)
            {
                if (value [0] == ' ' || value [0] == '\t')
                {
                    StringBuilder xBuilder = new StringBuilder ();

                    int xLength = value.Length;

                    for (int temp = 0; temp < xLength; temp ++)
                    {
                        char xCurrent = value [temp];

                        if (xCurrent == ' ')
                            xBuilder.Append (HalfWidthSpaceReplacementString);

                        else if (xCurrent == '\t')
                            xBuilder.Append (TabReplacementString);

                        else
                        {
                            xBuilder.Append (value.AsSpan (temp));
                            break;
                        }
                    }

                    return xBuilder.ToString ();
                }
            }

            return value;
        }
    }
}
