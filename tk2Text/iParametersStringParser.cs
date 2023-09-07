using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nekote;

namespace tk2Text
{
    internal class iParametersStringParser
    {
        public readonly string OriginalString;

        // 構文解析されるだけで、それらの正当性（たとえばパスが存在するか）は評価されない
        // 区切り文字が | なので Title に | を使えないのは、致命的なことでないので仕様とする

        public readonly IEnumerable <string> IncludedPaths;

        public readonly IEnumerable <string> ExcludedPaths;

        public readonly IEnumerable <iMergedPathsInfo> MergedPaths;

        public readonly IEnumerable <string> CategoryNames;

        public readonly IEnumerable <iAttributesInfo> Attributes;

        public readonly IEnumerable <Guid> ExcludedItems;

        public readonly IEnumerable <string> ErrorMessages;

        public bool IsEmpty
        {
            get
            {
                return (IncludedPaths.Any () || ExcludedPaths.Any () || MergedPaths.Any () ||
                    CategoryNames.Any () || Attributes.Any () || ExcludedItems.Any () || ErrorMessages.Any ()) == false;
            }
        }

        public iParametersStringParser (string originalString)
        {
            OriginalString = originalString;

            List <string>
                xIncludedPaths = new List <string> (),
                xExcludedPaths = new List <string> (),
                xCategoryNames = new List <string> (),
                xErrorMessages = new List <string> ();

            List <iMergedPathsInfo> xMergedPaths = new List <iMergedPathsInfo> ();

            List <iAttributesInfo> xAttributes = new List <iAttributesInfo> ();

            List <Guid> xExcludedItems = new List <Guid> ();

            foreach (string xLine in OriginalString.nSplitIntoLines ())
            {
                // Merge では C:\Hoge | C:\Moge のように複数のパスを | で並べることになるため、可読性のため各項目の前後に空白を置けるようにする
                // その仕様との整合性も考え、キー直前や行コメント直前の空白もトリミング
                // パスや Title に // を含めることが不可欠の状況は想定しにくい

                string xTrimmed;

                int xIndex = xLine.IndexOf ("//", StringComparison.Ordinal);

                if (xIndex >= 0)
                    xTrimmed = xLine.Substring (0, xIndex).Trim ();

                else xTrimmed = xLine.Trim ();

                if (xTrimmed.Length == 0)
                    continue;

                if (xTrimmed.StartsWith ("Include:", StringComparison.OrdinalIgnoreCase))
                {
                    string xValue = xTrimmed.Substring ("Include:".Length).TrimStart ();

                    if (Path.IsPathFullyQualified (xValue))
                    {
#if DEBUG
                        Console.WriteLine ("Parsed Include: " + xValue);
#endif
                        xIncludedPaths.Add (xValue);
                    }

                    else xErrorMessages.Add ("パラメーターが不正です: " + xTrimmed);
                }

                else if (xTrimmed.StartsWith ("Exclude:", StringComparison.OrdinalIgnoreCase))
                {
                    string xValue = xTrimmed.Substring ("Exclude:".Length).TrimStart ();

                    if (Path.IsPathFullyQualified (xValue))
                    {
#if DEBUG
                        Console.WriteLine ("Parsed Exclude: " + xValue);
#endif
                        xExcludedPaths.Add (xValue);
                    }

                    else if (Guid.TryParse (xValue, out Guid xResult))
                    {
#if DEBUG
                        Console.WriteLine ("Parsed Exclude: " + xResult.ToString ("D"));
#endif
                        xExcludedItems.Add (xResult);
                    }

                    else xErrorMessages.Add ("パラメーターが不正です: " + xTrimmed);
                }

                else if (xTrimmed.StartsWith ("Merge:", StringComparison.OrdinalIgnoreCase))
                {
                    var xValues = xTrimmed.Substring ("Merge:".Length).Split ('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                    if (xValues.Length >= 2 && xValues.All (x => Path.IsPathFullyQualified (x)))
                    {
#if DEBUG
                        // First は速そうなので値をキャッシュしない
                        Console.WriteLine (string.Join (Environment.NewLine, xValues.Skip (1).Select (x => $"Parsed Merge: {xValues.First ()} | {x}")));
#endif
                        xMergedPaths.AddRange (xValues.Skip (1).Select (x => new iMergedPathsInfo (xValues.First (), x)));
                    }

                    else xErrorMessages.Add ("パラメーターが不正です: " + xTrimmed);
                }

                else if (xTrimmed.StartsWith ("Category:", StringComparison.OrdinalIgnoreCase))
                {
                    string xValue = xTrimmed.Substring ("Category:".Length).TrimStart ();

                    if (string.IsNullOrEmpty (xValue) == false)
                    {
#if DEBUG
                        Console.WriteLine ("Parsed Category: " + xValue);
#endif
                        // Include などと異なり、同じ値が複数回入ると出力に影響がある

                        if (xCategoryNames.Contains (xValue, StringComparer.OrdinalIgnoreCase) == false)
                            xCategoryNames.Add (xValue);
                    }

                    else xErrorMessages.Add ("パラメーターが不正です: " + xTrimmed);
                }

                else if (xTrimmed.StartsWith ("Attributes:", StringComparison.OrdinalIgnoreCase))
                {
                    // タスクリストの Settings.txt から Title を引っ張る選択肢もあるが、
                    //     たとえば tkView のタスクリストの名前は「複数の taskKiller のタスクリストをまたぐダッシュボードのソフトを書く」であり、そのまま使いにくい
                    // また、複数のタスクリストを使っての開発において、一つ目は「○○を開発」、二つ目は「○○を更新」となっているなど、名前が整合しないこともある
                    // 基本、タスクリストごとに一度だけの手間なので、出力先パスとタイトルの個別の設定を必須とするのがシンプル

                    // その設定を忘れていれば、特定のディレクトリーに含まれる全てのタスクリストのログを変換するにおいて一つだけ未設定なことで全体が失敗する
                    // これは、ランタイムですぐ気づけてすぐに直せることなので、あえてそのような仕様にしている
                    // そうせず、タイトルなどが未設定ならデフォルト値でページが生成されるようにすると、寝ぼけているときに思わぬ情報が流出しうる

                    var xValues = xTrimmed.Substring ("Attributes:".Length).Split ('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                    if (xValues.Length == 6 && Path.IsPathFullyQualified (xValues [0]) && Path.IsPathFullyQualified (xValues [2]))
                    {
#if DEBUG
                        Console.WriteLine ($"Parsed Attributes: {xValues [0]} | {xValues [1]} | {xValues [2]} | {xValues [3]} | {xValues [4]} | {xValues [5]}");
#endif
                        xAttributes.Add (new iAttributesInfo (xValues [0], xValues [1], xValues [2], xValues [3], xValues [4], xValues [5]));
                    }

                    else xErrorMessages.Add ("パラメーターが不正です: " + xTrimmed);
                }
            }

            IncludedPaths = xIncludedPaths;
            ExcludedPaths = xExcludedPaths;
            MergedPaths = xMergedPaths;
            CategoryNames = xCategoryNames;
            Attributes = xAttributes;
            ExcludedItems = xExcludedItems;
            ErrorMessages = xErrorMessages;
        }
    }
}
