using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nekote;

namespace tk2Text
{
    internal class iTaskListDirectoryInfo
    {
        public readonly iParametersStringParser Parser;

        public readonly string Path;

        public iTaskListDirectoryInfo (iParametersStringParser parser, string path)
        {
            Parser = parser;
            Path = path;
        }

        private IEnumerable <TaskInfo>? mAllTasks;

        public IEnumerable <TaskInfo> AllTasks
        {
            get
            {
                if (mAllTasks == null)
                {
                    string xTasksDirectoryPath = nPath.Combine (Path, "Tasks");

                    // iParametersValidator で見るため不要だが、作法として

                    if (nDirectory.Exists (xTasksDirectoryPath) == false)
                        mAllTasks = Enumerable.Empty <TaskInfo> ();

                    else
                    {
                        List <TaskInfo> xTasks = new List <TaskInfo> ();

                        StateManager xStateManager = new StateManager (Path);
                        OrderManager xOrderManager = new OrderManager (Path);

                        foreach (string xFilePath in Directory.GetFiles (xTasksDirectoryPath, "*.*", SearchOption.TopDirectoryOnly))
                        {
                            try
                            {
                                TaskInfo xTask = Shared.LoadTask (xFilePath, xStateManager, xOrderManager);

                                if (Parser.ExcludedItems.Contains (xTask.Guid))
                                    continue;

                                // Shared.LoadTask の実装を変更せず、メモの一部を除外
                                // 無理やりな実装だが、実際に除外されるものが少ないのでコストは小さい

                                for (int temp = xTask.Notes.Count - 1; temp >= 0; temp --)
                                {
                                    NoteInfo xNote = xTask.Notes [temp];

                                    if (Parser.ExcludedItems.Contains (xNote.Guid))
                                        xTask.Notes.RemoveAt (temp);
                                }

                                xTasks.Add (xTask);
                            }

                            catch
                            {
                                // おかしいファイルは、このアプリでは無視される
                                // ほかのアプリでチェックされる
                            }
                        }

                        mAllTasks = xTasks;
                    }
                }

                return mAllTasks;
            }
        }

        private IEnumerable <iAttachedFileInfo>? mAttachedFiles;

        public IEnumerable <iAttachedFileInfo> AttachedFiles
        {
            get
            {
                if (mAttachedFiles == null)
                {
                    string xFilesDirectoryPath = nPath.Combine (Path, "Files"),
                        xInfoFilePath = nPath.Combine (Path, "Files", "Info.txt");

                    // いきなり Info.txt を見てもよいが、一応

                    if (nDirectory.Exists (xFilesDirectoryPath) == false || nFile.Exists (xInfoFilePath) == false)
                        mAttachedFiles = Enumerable.Empty <iAttachedFileInfo> ();

                    else
                    {
                        List <iAttachedFileInfo> xFiles = new List <iAttachedFileInfo> ();

                        foreach (string xParagraph in nFile.ReadAllText (xInfoFilePath).nSplitIntoParagraphs ())
                        {
                            string [] xLines = xParagraph.nSplitIntoLines ();

                            if (xLines.Length != 5)
                                continue;

                            if (xLines [0].Length <= 2 || xLines [0][0] != '[' || xLines [0][xLines [0].Length - 1] != ']')
                                continue;

                            // 今のところ相対パスは必ず Files/ で始まるが、外部ファイルへのリンクを可能にする可能性もあるし、
                            //     いずれにしてもファイルの存在チェックをするので、Files/ で始まっているかどうかまでは見ない

                            string xRelativePath = xLines [0].Substring (1, xLines [0].Length - 2),
                                xFullPath = nPath.Combine (Path, xRelativePath);

                            if (nFile.Exists (xFullPath) == false)
                                continue;

                            if (xLines [1].StartsWith ("Guid:", StringComparison.OrdinalIgnoreCase) == false ||
                                    Guid.TryParse (xLines [1].AsSpan ("Guid:".Length), out Guid xGuid) == false)
                                continue;

                            if (xLines [2].StartsWith ("ParentGuid:", StringComparison.OrdinalIgnoreCase) == false)
                                continue;

                            // ParentGuid は、空か有効な文字列か
                            // 長さが1以上の場合のみチェックし、問題があれば抜ける

                            Guid? xParentGuid = null;

                            if (xLines [2].Length > "ParentGuid:".Length)
                            {
                                if (Guid.TryParse (xLines [2].AsSpan ("ParentGuid:".Length), out Guid xParentGuidAlt) == false)
                                    continue;

                                xParentGuid = xParentGuidAlt;
                            }

                            if (xLines [3].StartsWith ("AttachedAt:", StringComparison.OrdinalIgnoreCase) == false ||
                                DateTime.TryParseExact (xLines [3].AsSpan ("AttachedAt:".Length), "O",
                                    CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime xAttachedAtUtc) == false)
                                continue;

                            if (xLines [4].StartsWith ("ModifiedAt:", StringComparison.OrdinalIgnoreCase) == false ||
                                DateTime.TryParseExact (xLines [4].AsSpan ("ModifiedAt:".Length), "O",
                                    CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime xModifiedAtUtc) == false)
                                continue;

                            xFiles.Add (new iAttachedFileInfo (xFullPath, xGuid, xParentGuid, xAttachedAtUtc, xModifiedAtUtc));

                            // タスクのファイルと同様、おかしいものは単純にスルーされる
                            // 今のところ添付ファイルを編集する機能が taskKiller にないため、
                            //     Info.txt の情報との不整合などの可能性は低い
                        }

                        mAttachedFiles = xFiles;
                    }
                }

                return mAttachedFiles;
            }
        }
    }
}
