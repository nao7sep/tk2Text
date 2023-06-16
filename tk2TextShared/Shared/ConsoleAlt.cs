using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Nekote;

namespace tk2Text
{
    public static class ConsoleAlt
    {
        public static readonly string OldMessagesFilePath = Path.Join (Path.GetDirectoryName (Assembly.GetEntryAssembly ()!.Location), "OldMessages.txt");

        private static List <string>? mOldMessages = null;

        public static List <string> OldMessages
        {
            get
            {
                if (mOldMessages == null)
                {
                    mOldMessages = new List <string> ();

                    if (nFile.Exists (OldMessagesFilePath))
                        mOldMessages.AddRange (nFile.ReadAllLinesEnumerable (OldMessagesFilePath).Where (x => x.Length > 0).Distinct (StringComparer.Ordinal));
                }

                return mOldMessages;
            }
        }

        public static void WriteLine (string value, bool oneTimeOnly, ConsoleColor? backgroundColor, ConsoleColor? foregroundColor)
        {
            if (oneTimeOnly)
            {
                if (OldMessages.Contains (value, StringComparer.Ordinal))
                    return;

                else
                {
                    // 改行を考慮しないので、改行を含まない文字列しか処理できない

                    OldMessages.Add (value);
                    nFile.AppendAllText (OldMessagesFilePath, value + Environment.NewLine);
                }
            }

            if (backgroundColor != null || foregroundColor != null)
            {
                if (backgroundColor != null)
                    Console.BackgroundColor = backgroundColor.Value;

                if (foregroundColor != null)
                    Console.ForegroundColor = foregroundColor.Value;

                Console.WriteLine (value);
                Console.ResetColor ();
            }

            else Console.WriteLine (value);
        }

        public static void WriteInfoLine (string value)
        {
            WriteLine (value, false, ConsoleColor.Blue, ConsoleColor.White);
        }

        public static void WriteErrorLine (string value)
        {
            WriteLine (value, false, ConsoleColor.Red, ConsoleColor.White);
        }

        public static void WriteOneTimeOnlyLine (string value)
        {
            WriteLine (value, true, ConsoleColor.Yellow, ConsoleColor.Black);
        }
    }
}
