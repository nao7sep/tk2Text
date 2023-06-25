using System.Reflection;
using System.Text;
using Nekote;

namespace tk2Text
{
    internal class Program
    {
        static void Main (/* string [] args */)
        {
            try
            {
                // 存在しなければ Parameters.txt を出力し、段落分けし、それぞれの1行目を taskKiller のディレクトリーパス、
                //     2行目を出力するテキストファイルのパス、それ以降を / から始まる順不同のオプションとして認識

                string xParametersFilePath = Path.Join (Path.GetDirectoryName (Assembly.GetEntryAssembly ()!.Location), "Parameters.txt");

                if (File.Exists (xParametersFilePath) == false)
                    nFile.Create (xParametersFilePath);

                foreach (string xParagraph in nFile.ReadAllText (xParametersFilePath).nSplitIntoParagraphs ())
                {
                    string [] xLines = xParagraph.nSplitIntoLines ();

                    // テキストファイルの方ではファイル名も含むパスを受け取るが、
                    //     上位ディレクトリーがないなら、そのプロジェクトは終わっている可能性が高い

                    if (xLines.Length >= 2 &&
                        Path.IsPathFullyQualified (xLines [0]) && nDirectory.Exists (xLines [0]) &&
                        Path.IsPathFullyQualified (xLines [1]) && nDirectory.Exists (nPath.GetDirectoryPath (xLines [1])))
                    {
                        Console.WriteLine ("タスクリスト: " + xLines [0]);

                        var xSettings = Shared.LoadSettings (xLines [0]);

                        if (xSettings == null || xSettings.Count == 0)
                            ConsoleAlt.WriteErrorLine ("設定のロードに失敗しました: " + Path.Join (xLines [0], "Settings.txt"));

                        List <string> xFailedFilePaths = Shared.LoadTasks (xLines [0], out List <TaskInfo> xResult);

                        if (xFailedFilePaths.Count == 1)
                            ConsoleAlt.WriteErrorLine ("タスクのロードに失敗しました: " + xFailedFilePaths [0]);

                        else if (xFailedFilePaths.Count >= 2)
                        {
                            ConsoleAlt.WriteErrorLine ("タスクのロードに失敗しました:");

                            ConsoleAlt.WriteErrorLine (string.Join (Environment.NewLine, xFailedFilePaths.
                                OrderBy (x => x, StringComparer.OrdinalIgnoreCase).
                                Select (x => Shared.Indent + x)));
                        }

                        List <Guid> xExcludedTasksAndNotes = new List <Guid> ();

                        for (int temp = 2; temp < xLines.Length; temp ++)
                        {
                            string xLine = xLines [temp];

                            if (xLine.StartsWith ("//", StringComparison.Ordinal))
                                continue;

                            if (xLine.StartsWith ("/Exclude:", StringComparison.OrdinalIgnoreCase))
                            {
                                if (Guid.TryParse (xLine.AsSpan ("/Exclude:".Length), out Guid xResultAlt))
                                {
                                    xExcludedTasksAndNotes.Add (xResultAlt);
                                    continue;
                                }
                            }

                            ConsoleAlt.WriteErrorLine ("オプションを認識できません: " + xLine);
                        }

                        StringBuilder xBuilder = new StringBuilder ();

                        xBuilder.AppendLine (Shared.ThickBorderLine);

                        if (xSettings != null && xSettings.ContainsKey ("Title"))
                            xBuilder.AppendLine (Shared.Indent + xSettings ["Title"]);

                        else xBuilder.AppendLine (Shared.Indent + nPath.GetName (xLines [0]));

                        xBuilder.AppendLine (Shared.ThickBorderLine);

                        var xSortedHandledTasks = Shared.SortTasks (xResult.Where (x => x.State == TaskState.Done || x.State == TaskState.Cancelled));

                        if (xSortedHandledTasks.Any ())
                        {
                            xBuilder.AppendLine ();

                            foreach (TaskInfo xTask in xSortedHandledTasks)
                            {
                                if (xExcludedTasksAndNotes.Contains (xTask.Guid))
                                {
                                    ConsoleAlt.WriteOneTimeOnlyLine ("タスクを除外しました: " + xTask.Guid.nToString ());
                                    continue;
                                }

                                xBuilder.AppendLine ($"[{Shared.StateToString (xTask.State)}] {xTask.Contents}");

                                if (xTask.Notes.Count > 0)
                                {
                                    bool xIsFirstNote = true;
                                    int xNoteCount = 0;

                                    foreach (NoteInfo xNote in Shared.SortNotes (xTask.Notes))
                                    {
                                        if (xExcludedTasksAndNotes.Contains (xNote.Guid))
                                        {
                                            ConsoleAlt.WriteOneTimeOnlyLine ("メモを除外しました: " + xNote.Guid.nToString ());
                                            continue;
                                        }

                                        if (xIsFirstNote)
                                            xIsFirstNote = false;

                                        else
                                        {
                                            xBuilder.AppendLine ();
                                            xBuilder.AppendLine (Shared.Indent + Shared.ThinBorderLine);
                                        }

                                        xBuilder.AppendLine ();
                                        xBuilder.AppendLine (Shared.IndentLines (xNote.Contents!, 1));
                                        xNoteCount ++;
                                    }

                                    if (xNoteCount > 0)
                                        xBuilder.AppendLine ();
                                }
                            }
                        }

                        // ファイルの最後がタスクでなくメモのときに余計な空行が入るなどに一発で対処
                        string xNewFileContents = xBuilder.ToString ().TrimEnd () + Environment.NewLine;

                        if (nFile.Exists (xLines [1]) == false)
                        {
                            nFile.WriteAllText (xLines [1], xNewFileContents);
                            ConsoleAlt.WriteInfoLine ("作成しました: " + xLines [1]);
                        }

                        else
                        {
                            string xCurrentFileContents = nFile.ReadAllText (xLines [1]);

                            if (xNewFileContents != xCurrentFileContents)
                            {
                                nFile.WriteAllText (xLines [1], xNewFileContents);
                                ConsoleAlt.WriteInfoLine ("更新しました: " + xLines [1]);
                            }

                            else Console.WriteLine ("既に最新です: " + xLines [1]);
                        }
                    }
                }
            }

            catch (Exception xException)
            {
                ConsoleAlt.WriteErrorLine ("エラーが発生しました:");
                ConsoleAlt.WriteErrorLine (Shared.IndentLines (xException.ToString ().TrimEnd (), 1));
            }

            Console.Write ("プログラムを終了するには、任意のキーを押してください: ");
            Console.ReadKey (true);
            Console.WriteLine ();
        }
    }
}
