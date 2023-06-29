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
                    Console.WriteLine ("Parameters.txt を設定してください。");
                    goto End;
                }

                string [] xParagraphs = nFile.ReadAllText (xParametersFilePath).nSplitIntoParagraphs ();

                if (xParagraphs.Length == 0)
                {
                    Console.WriteLine ("Parameters.txt を設定してください。");
                    goto End;
                }

                foreach (string xParagraph in xParagraphs)
                {
                    iParametersStringParser xParser = new iParametersStringParser (xParagraph);

                    if (xParser.ErrorMessages.Count () > 0)
                    {
                        Console.WriteLine (string.Join (Environment.NewLine, xParser.ErrorMessages));
                        goto End;
                    }

                    iParametersValidator xValidator = new iParametersValidator (xParser);

                    if (xValidator.ErrorMessages.Count () > 0)
                    {
                        Console.WriteLine (string.Join (Environment.NewLine, xValidator.ErrorMessages));
                        goto End;
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
