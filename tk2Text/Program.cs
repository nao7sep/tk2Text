using System.Reflection;
using Nekote;

namespace tk2Text
{
    internal class Program
    {
        static void Main (/* string [] args */)
        {
            try
            {
                string xParametersFilePath = nPath.Combine (nPath.GetDirectoryPath (Assembly.GetExecutingAssembly ().Location), "Parameters.txt");

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

                foreach (string xParagraph in xParagraphs)
                {
                    iParametersStringParser xParser = new iParametersStringParser (xParagraph);

                    if (xParser.ErrorMessages.Any ())
                    {
                        ConsoleAlt.WriteErrorLine (string.Join (Environment.NewLine, xParser.ErrorMessages));
                        goto End;
                    }

                    iParametersValidator xValidator = new iParametersValidator (xParser);

                    if (xValidator.ErrorMessages.Any ())
                    {
                        ConsoleAlt.WriteErrorLine (string.Join (Environment.NewLine, xValidator.ErrorMessages));
                        goto End;
                    }

                    foreach (iMergedTaskListInfo xMergedTaskList in xValidator.MergedTaskLists)
                    {
                        iHtmlPageGenerator xHtmlPageGenerator = new iHtmlPageGenerator (xParser, xMergedTaskList);

                        if (xHtmlPageGenerator.TryGenerate () == false)
                            ConsoleAlt.WriteErrorLine (string.Join (Environment.NewLine, xHtmlPageGenerator.ErrorMessages));
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
