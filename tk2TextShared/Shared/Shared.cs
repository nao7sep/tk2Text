using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nekote;

namespace tk2Text
{
    public static class Shared
    {
        public static List <string> SplitIntoParagraphs (string value)
        {
            List <string> xParagraphs = new List <string> ();
            StringBuilder xBuilder = new StringBuilder ();

            using (StringReader xReader = new StringReader (value))
            {
                string? xLine;

                while ((xLine = xReader.ReadLine ()) != null)
                {
                    if (xLine.Length > 0)
                    {
                        if (xBuilder.Length > 0)
                            xBuilder.AppendLine ();

                        xBuilder.Append (xLine);
                    }

                    else
                    {
                        if (xBuilder.Length > 0)
                        {
                            xParagraphs.Add (xBuilder.ToString ());
                            xBuilder.Clear ();
                        }
                    }
                }

                if (xBuilder.Length > 0)
                {
                    xParagraphs.Add (xBuilder.ToString ());
                    // xParagraph.Clear ();
                }
            }

            return xParagraphs;
        }

        public static Dictionary <string, string> ParseKeyValueCollection (string value)
        {
            // キーの大文字・小文字は区別されない
            Dictionary <string, string> xDictionary = new Dictionary <string, string> (StringComparer.OrdinalIgnoreCase);

            using (StringReader xReader = new StringReader (value))
            {
                string? xLine;

                while ((xLine = xReader.ReadLine ()) != null)
                {
                    int xIndex = xLine.IndexOf (':');

                    // キーの長さが1以上

                    if (xIndex <= 0)
                        throw new FormatException ();

                    string xKey = xLine.Substring (0, xIndex),
                        xValue = xLine.Substring (xIndex + 1);

                    if (xDictionary.ContainsKey (xKey))
                        throw new InvalidDataException ();

                    xDictionary.Add (xKey, xValue);
                }
            }

            return xDictionary;
        }

        public static Dictionary <string, string>? LoadSettings (string appDirectoryPath)
        {
            string xSettingsFilePath = Path.Join (appDirectoryPath, "Settings.txt");

            // LoadTasks と同様、読めないなら戻り値でそれが分かり、
            //     そもそもファイルがないなら空のインスタンスが返ってくる

            if (File.Exists (xSettingsFilePath))
            {
                try
                {
                    return ParseKeyValueCollection (File.ReadAllText (xSettingsFilePath, Encoding.UTF8));
                }

                catch
                {
                    return null;
                }
            }

            else return new Dictionary <string, string> (StringComparer.OrdinalIgnoreCase);
        }

        public static TaskInfo LoadTask (string filePath, StateManager stateManager, OrderManager orderManager)
        {
            var xParagraphs = SplitIntoParagraphs (File.ReadAllText (filePath, Encoding.UTF8));

            if (xParagraphs.Count == 0)
                throw new InvalidDataException ();

            TaskInfo xTask = new TaskInfo ();

            Dictionary <string, string> xDictionary = ParseKeyValueCollection (xParagraphs [0]);

            // フォーマットの問題としていたが、データの問題とした方が良さそう
            // たとえば Format:taskKiller2 があるなら、ここで taskKiller1 と一致しないのは、
            //     解析に不都合を生じさせるフォーマットの問題でなく、別のデータが混入しただけのこと

            if (xDictionary ["Format"] != "taskKiller1")
                throw new InvalidDataException ();

            xTask.Guid = Guid.Parse (xDictionary ["Guid"]);
            xTask.CreationUtc = long.Parse (xDictionary ["CreationUtc"]);
            xTask.Contents = xDictionary ["Content"].nUnescapeC ();

            if (xDictionary ["State"] != "Queued")
                xTask.State = (TaskState) Enum.Parse (typeof (TaskState), xDictionary ["State"]);

            if (stateManager.ContainsKey (xDictionary ["Guid"]))
                xTask.State = stateManager.SafeGetValue (xDictionary ["Guid"]);

            if (xDictionary.ContainsKey ("HandlingUtc") && xDictionary ["HandlingUtc"].Length > 0)
                xTask.HandlingUtc = long.Parse (xDictionary ["HandlingUtc"]);

            if (xDictionary.ContainsKey ("RepeatedGuid") && xDictionary ["RepeatedGuid"].Length > 0)
                xTask.RepeatedTaskGuid = Guid.Parse (xDictionary ["RepeatedGuid"]);

            xTask.OrderingUtc = -1;

            if (xDictionary.ContainsKey ("OrderingUtc"))
                xTask.OrderingUtc = long.Parse (xDictionary ["OrderingUtc"]);

            if (orderManager.ContainsKey (xDictionary ["Guid"]))
                xTask.OrderingUtc = orderManager.SafeGetUtc (xDictionary ["Guid"]);

            if (xTask.OrderingUtc < 0)
                xTask.IsSpecial = true;

            for (int temp = 1; temp < xParagraphs.Count; temp ++)
            {
                NoteInfo xNote = new NoteInfo ();

                xDictionary = ParseKeyValueCollection (xParagraphs [temp]);

                xNote.Guid = Guid.Parse (xDictionary ["Guid"]);
                xNote.CreationUtc = long.Parse (xDictionary ["CreationUtc"]);
                xNote.Contents = xDictionary ["Content"].nUnescapeC ();
                xNote.ParentTask = xTask;

                xTask.Notes.Add (xNote);
            }

            return xTask;
        }

        public static List <string> LoadTasks (string appDirectoryPath, out List <TaskInfo> result)
        {
            List <TaskInfo> xTasks = new List <TaskInfo> ();

            string xTasksDirectoryPath = Path.Join (appDirectoryPath, "Tasks");

            List <string> xFailedFilePaths = new List <string> ();

            if (Directory.Exists (xTasksDirectoryPath))
            {
                StateManager xStateManager = new StateManager (appDirectoryPath);
                OrderManager xOrderManager = new OrderManager (appDirectoryPath);

                foreach (FileInfo xFile in new DirectoryInfo (xTasksDirectoryPath).GetFiles ("*.txt"))
                {
                    try
                    {
                        TaskInfo xTask = LoadTask (xFile.FullName, xStateManager, xOrderManager);
                        xTasks.Add (xTask);
                    }

                    catch
                    {
                        xFailedFilePaths.Add (xFile.FullName);
                    }
                }
            }

            result = xTasks;

            return xFailedFilePaths;
        }

        public static IOrderedEnumerable <TaskInfo> SortTasks (IEnumerable <TaskInfo> tasks)
        {
            return tasks.OrderBy (x => x, new TaskOrderComparer ());
        }

        public static IOrderedEnumerable <NoteInfo> SortNotes (IEnumerable <NoteInfo> notes)
        {
            return notes.OrderBy (x => x.CreationUtc);
        }

        public static readonly string ThickBorderLine = new string ('=', 80);

        public static readonly string ThinBorderLine = new string ('-', 80);

        public static readonly string Indent = "\x20\x20\x20\x20";

        public static string StateToString (TaskState state)
        {
            return state switch
            {
                TaskState.Later => "あとで",
                TaskState.Soon => "早めに",
                TaskState.Now => "今すぐ",
                TaskState.Done => "完了",
                TaskState.Cancelled => "却下",
                _ => throw new InvalidDataException ()
            };
        }

        public static string IndentLines (string value, int depth)
        {
            string xIndents = string.Concat (Enumerable.Repeat (Indent, depth));

            return string.Join (Environment.NewLine, value.nSplitIntoLines ().Select (x =>
            {
                if (x.Length > 0)
                    return xIndents + x;

                else return x;
            }));
        }
    }
}
