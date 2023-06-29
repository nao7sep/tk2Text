using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nekote;

namespace tk2Text
{
    // iMergedTaskListInfo に実装してもよいが、将来的な複数フォーマットへの対応およびそれぞれでのパラメーター指定を想定

    internal class iHtmlPageGenerator
    {
        public readonly iParametersStringParser Parser;

        public readonly iMergedTaskListInfo MergedTaskList;

        public IEnumerable <string> ErrorMessages;

        public iHtmlPageGenerator (iParametersStringParser parser, iMergedTaskListInfo mergedTaskList)
        {
            Parser = parser;
            MergedTaskList = mergedTaskList;
            ErrorMessages = new List <string> ();
        }

        public bool TryGenerate ()
        {
            StringBuilder xBuilder = new StringBuilder ();

            string xFileContents = xBuilder.ToString ();

            // ファイルの内容が存在する場合のみ、また、ファイルが既存ならその内容と異なる場合のみ書き込む
            // 出力するログがない場合にページの側を消すまでのことは、今のところしない

            if (xFileContents.Length > 0)
            {
                string? xOldFileContents = null;

                if (nFile.Exists (MergedTaskList.DestFilePath))
                    xOldFileContents = nFile.ReadAllText (MergedTaskList.DestFilePath);

                if (string.Equals (xFileContents, xOldFileContents, StringComparison.Ordinal) == false)
                {
                    ConsoleAlt.WriteInfoLine ($"ページを{(xOldFileContents != null ? "更新" : "作成")}しました: {MergedTaskList.DestFilePath}");
                    nFile.WriteAllText (MergedTaskList.DestFilePath, xFileContents);
                }
            }

            // 成功したかどうかを示す
            // 上書きの必要性がない場合も成功
            return true;
        }
    }
}
