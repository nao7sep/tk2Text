using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nekote;

namespace tk2Text
{
    internal class iParametersValidator
    {
        public readonly iParametersStringParser Parser;

        public readonly IEnumerable <iMergedTaskListInfo> MergedTaskLists;

        public readonly IEnumerable <string> ErrorMessages;

        public iParametersValidator (iParametersStringParser parser)
        {
            Parser = parser;

            List <string> xErrorMessages = new List <string> ();

            var xAllPaths = Parser.IncludedPaths.SelectMany (x =>
            {
                // タスクリストのディレクトリーのアーカイブを想定し、ディレクトリーがなくなっていてもエラーにしない
                // しかし、結果として一つも処理対象がないなら、あとでエラーメッセージが出力される
                // 一つ以上のタスクリストが含まれるディレクトリーだけでなく、タスクリストのディレクトリーそのものの指定も想定

                if (nDirectory.Exists (x))
                {
                    List <string> xPaths = new List <string> ();

                    // アーカイブしたタスクリストを一時的に展開したのであっても検出できるよう、taskKiller.exe でなく Tasks を見る

                    if (nDirectory.Exists (nPath.Combine (x, "Tasks")))
                        xPaths.Add (x);

                    xPaths.AddRange (Directory.GetDirectories (x, "*", SearchOption.AllDirectories).
                        Where (y => nDirectory.Exists (nPath.Combine (y, "Tasks"))));

                    return xPaths;
                }

                else return Enumerable.Empty <string> ();
            }).
            Distinct (StringComparer.OrdinalIgnoreCase).
            Where (y => Parser.ExcludedPaths.Contains (y, StringComparer.OrdinalIgnoreCase) == false);
#if DEBUG
            if (xAllPaths.Count () > 0)
                Console.WriteLine (string.Join (Environment.NewLine, xAllPaths.Select (x => $"DirectoryPath: {x}")));
#endif
            if (xAllPaths.Count () == 0)
                xErrorMessages.Add ("処理対象のタスクリストが一つもありません。");

            // 「右のパスを左のパスに統合する」のペア情報の集まり
            // 左対右が一対多
            // 右でグループ化して左が二つ以上のところを探す

            if (Parser.MergedPaths.GroupBy (x => x.RightPath, StringComparer.OrdinalIgnoreCase).Any (y => y.Count () >= 2))
                xErrorMessages.Add ("タスクリストの統合の設定に重複があります。");

            // 属性情報の重複まではチェックしない
            // めんどくさいし、FirstOrDefault により最初に見つかったものだけが使われるため
            // たとえば A と B のタスクリストが統合されるにおいて、A の Title は Hoge、B の Title は Moge と指定すれば、
            //     Hoge を含む指定と Moge を含む指定のうち先に見つかったものが A と B の両方に適用される

            iMergedPathsEqualityComparer xComparer = new iMergedPathsEqualityComparer (Parser.MergedPaths);

            MergedTaskLists = xAllPaths.GroupBy (x => x, xComparer).Select (y =>
            {
#if DEBUG
                if (y.Count () >= 2)
                    Console.WriteLine ("MergedDirectoryPaths: " + string.Join (" | ", y));
#endif
                // 属性情報を順に見ていき、SourcePath が一つ以上のディレクトリーパスのいずれかと一致する最初のエントリーを探す
                // 見つからなければ、SourcePath など全てが string.Empty の iAttributesInfo.Empty が得られる
                var xAttributes = Parser.Attributes.FirstOrDefault (z => y.Contains (z.SourcePath, StringComparer.OrdinalIgnoreCase), iAttributesInfo.Empty);

                if (string.IsNullOrEmpty (xAttributes.SourcePath))
                    xErrorMessages.Add ("属性情報がありません: " + string.Join (" | ", y));
#if DEBUG
                else Console.WriteLine ($"Attributes: {string.Join (" | ", y)} | {xAttributes.DestPath} | {xAttributes.Title}");
#endif
                iMergedTaskListInfo xMergedTaskList = new iMergedTaskListInfo (xAttributes.DestPath, xAttributes.Title);
                xMergedTaskList.Directories.AddRange (y.Select (z => new iTaskListDirectoryInfo (z)));
                return xMergedTaskList;
            });
#if DEBUG
            // エラーメッセージがあって処理が打ち切られると、LINQ の処理が行われず、デバッグ用の情報が表示されない
            MergedTaskLists.ToArray ();
#endif
            ErrorMessages = xErrorMessages;
        }
    }
}
