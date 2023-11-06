using Nekote;

namespace tk2Text
{
    internal class Program
    {
        static void Main (/* string [] args */)
        {
            try
            {
                string xParametersFilePath = nPath.Combine (iShared.AppDirectoryPath, "Parameters.txt");

                if (nFile.Exists (xParametersFilePath) == false)
                {
                    nFile.Create (xParametersFilePath);
                    ConsoleAlt.WriteInfoLine ("Parameters.txt を設定してください。");
                    goto End;
                }

                string [] xParagraphs = nFile.ReadAllText (xParametersFilePath).nSplitIntoParagraphs ();

                if (xParagraphs.Length == 0)
                {
                    ConsoleAlt.WriteInfoLine ("Parameters.txt を設定してください。");
                    goto End;
                }

                string xReplacementsFilePath = nPath.Combine (iShared.AppDirectoryPath, "Replacements.txt");

                if (nFile.Exists (xReplacementsFilePath) == false)
                    nFile.Create (xReplacementsFilePath);

                iStringReplacer xReplacer = new iStringReplacer (xReplacementsFilePath);

                foreach (string xParagraph in xParagraphs)
                {
                    iParametersStringParser xParser = new iParametersStringParser (xParagraph);

                    // コメント行だけの段落がエラーにならないように

                    if (xParser.IsEmpty)
                        continue;

                    if (xParser.ErrorMessages.Any ())
                    {
                        ConsoleAlt.WriteErrorLine (string.Join (Environment.NewLine, xParser.ErrorMessages));
                        goto End;
                    }

                    if (xParser.WarningMessages.Any ())
                        ConsoleAlt.WriteInfoLine (string.Join (Environment.NewLine, xParser.WarningMessages));

                    iParametersValidator xValidator = new iParametersValidator (xParser);

                    if (xValidator.ErrorMessages.Any ())
                    {
                        ConsoleAlt.WriteErrorLine (string.Join (Environment.NewLine, xValidator.ErrorMessages));
                        goto End;
                    }

                    if (xValidator.WarningMessages.Any ())
                        ConsoleAlt.WriteInfoLine (string.Join (Environment.NewLine, xValidator.WarningMessages));

                    foreach (iMergedTaskListInfo xMergedTaskList in xValidator.MergedTaskLists.OrderBy (x => x.Attributes.DestFilePath, StringComparer.OrdinalIgnoreCase))
                    {
                        // 属性情報のないエントリーを無視
                        // チェック方法は、属性情報があるか調べるところと同じ

                        if (string.IsNullOrEmpty (xMergedTaskList.Attributes.SourceDirectoryPath))
                            continue;

                        iHtmlPageGenerator xGenerator = new iHtmlPageGenerator (xParser, xReplacer, xMergedTaskList);

                        if (xGenerator.TryGenerate (out iPageGenerationResult xResult))
                        {
                            if (xResult == iPageGenerationResult.Created)
                                ConsoleAlt.WriteInfoLine ("ページが作成されました: " + xMergedTaskList.Attributes.DestFilePath);

                            else if (xResult == iPageGenerationResult.Updated)
                                ConsoleAlt.WriteInfoLine ("ページが更新されました: " + xMergedTaskList.Attributes.DestFilePath);

                            else if (xResult == iPageGenerationResult.Unchanged)
                                Console.WriteLine ("ページが既に最新です: " + xMergedTaskList.Attributes.DestFilePath);
                        }

                        else ConsoleAlt.WriteErrorLine (string.Join (Environment.NewLine, xGenerator.ErrorMessages));
                    }
                }
            }

            catch (Exception xException)
            {
                ConsoleAlt.WriteErrorLine ("エラーが発生しました:");
                ConsoleAlt.WriteErrorLine (Shared.IndentLines (xException.ToString ().TrimEnd (), 1));
            }

        End:
            Console.Write ("プログラムを終了するには、任意のキーを押してください: ");
            Console.ReadKey (true);
            Console.WriteLine ();
        }
    }
}
