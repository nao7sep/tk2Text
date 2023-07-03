using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Nekote;

namespace tk2Text
{
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

        public bool TryGenerate (out iPageGenerationResult result)
        {
            List <string> xErrorMessages = (List <string>) ErrorMessages;

            // 実装がややこしくなるし、たいてい作業ミスだろうから、同じファイルが複数回添付されている場合に対処しない
            // ページを見る人の分かりやすさのために同じファイルや画像を複数のタスクやメモに表示する運用は今のところ考えにくい

            List <(string DestRelativeFilePath, string DestFilePath, iAttachedFileInfo File)> xHandledAttachedFiles =
                new List <(string DestRelativeFilePath, string DestFilePath, iAttachedFileInfo File)> ();

            foreach (iAttachedFileInfo xAttachedFile in MergedTaskList.AttachedFiles.OrderBy (x => x.AttachedAtUtc))
            {
                for (int temp = 0; ; temp ++)
                {
                    string xAttachedFileDestRelativePath,
                        xAttachedFileDestPath;

                    if (temp == 0)
                    {
                        xAttachedFileDestRelativePath = xAttachedFile.File.Name;
                        xAttachedFileDestPath = nPath.Combine (MergedTaskList.Attributes.AttachedFileDirectoryPath, xAttachedFileDestRelativePath);
                    }

                    else
                    {
                        xAttachedFileDestRelativePath = nPath.Combine (temp.ToString (CultureInfo.InvariantCulture), xAttachedFile.File.Name);
                        xAttachedFileDestPath = nPath.Combine (MergedTaskList.Attributes.AttachedFileDirectoryPath, xAttachedFileDestRelativePath);
                    }

                    if (xHandledAttachedFiles.All (x => string.Equals (x.DestFilePath, xAttachedFileDestPath, StringComparison.OrdinalIgnoreCase) == false))
                    {
                        try
                        {
#if DEBUG
                            bool xFileExisted = false;
#endif
                            if (nFile.Exists (xAttachedFileDestPath))
                            {
#if DEBUG
                                xFileExisted = true;
#endif
                                FileInfo xDestFile = new FileInfo (xAttachedFileDestPath);

                                // 長さが一致し、タイムスタンプも近いなら、taskKiller の添付ファイルなら、同一のファイルと見なしてよい
                                // 長さもタイムスタンプも同じで一部のみ内容の異なるファイルが入り込む可能性は極めて低い

                                // 3秒以内の実装なのは、ファイルシステムによりタイムスタンプの精度が異なるため
                                // OneDrive や Dropbox といったオンラインストレージにより「秒」未満が変わる可能性もある

                                if (xDestFile.Length == xAttachedFile.File.Length &&
                                    Math.Abs ((xDestFile.LastWriteTimeUtc - xAttachedFile.ModifiedAtUtc).TotalSeconds) <= 3)
                                {
#if DEBUG
                                    Console.WriteLine ("Unchanged Attached File: " + xAttachedFileDestPath);
#endif
                                    break;
                                }
                            }

                            nDirectory.CreateForFile (xAttachedFileDestPath);
                            xAttachedFile.File.CopyTo (xAttachedFileDestPath, true);
                            nFile.SetLastWriteUtc (xAttachedFileDestPath, xAttachedFile.ModifiedAtUtc);
#if DEBUG
                            if (xFileExisted == false)
                                Console.WriteLine ("Created Attached File: " + xAttachedFileDestPath);

                            else Console.WriteLine ("Updated Attached File: " + xAttachedFileDestPath);
#endif
                            break;
                        }

                        catch
                        {
                            xErrorMessages.Add ("添付ファイルの読み書きに失敗しました: " + xAttachedFileDestPath);

                            // 続けても仕方ないのでメソッドを抜ける

                            result = default;
                            return false;
                        }

                        finally
                        {
                            xHandledAttachedFiles.Add ((xAttachedFileDestRelativePath, xAttachedFileDestPath, xAttachedFile));
                        }
                    }
                }
            }

            if (nDirectory.Exists (MergedTaskList.Attributes.AttachedFileDirectoryPath))
            {
                string [] xUnhandledAttachedFilePaths = Directory.GetFiles (MergedTaskList.Attributes.AttachedFileDirectoryPath, "*.*", SearchOption.AllDirectories).
                    Where (x => xHandledAttachedFiles.All (y => string.Equals (y.DestFilePath, x, StringComparison.OrdinalIgnoreCase) == false)).
                    ToArray (); // LINQ が複数回処理されるのを回避

                if (xUnhandledAttachedFilePaths.Length > 0)
                {
                    xErrorMessages.AddRange (xUnhandledAttachedFilePaths.Select (x => $"古い添付ファイルが残っています: {x}"));

                    // こちらは、そのままページを生成しても処理において特に問題がないが、
                    //     すぐに対処してもらわないとユーザーが忘れうるので、いったん打ち切る

                    result = default;
                    return false;
                }
            }

            iHtmlStringBuilder xBuilder = new iHtmlStringBuilder ();

            xBuilder.OpenTag ("html");

            xBuilder.OpenTag ("head");
            xBuilder.AddTag ("title", safeValue: WebUtility.HtmlEncode (MergedTaskList.Attributes.Title));
            xBuilder.CloseTag (); // head

            xBuilder.OpenTag ("body");

            xBuilder.AddTag ("div", new [] { "class", "title" }, WebUtility.HtmlEncode (MergedTaskList.Attributes.Title));

            void iAddAttachedFilesPart (Guid? parentGuid)
            {
                var xAttachedFiles = xHandledAttachedFiles.Where (x => x.File.ParentGuid == parentGuid).ToArray ();

                if (xAttachedFiles.Length > 0)
                {
                    xBuilder.OpenTag ("div", new [] { "class", "files" });

                    foreach (var xAttachedFile in xAttachedFiles)
                    {
                        xBuilder.OpenTag ("div", new [] { "class", "file" });

                        // todo

                        xBuilder.CloseTag (); // div.file
                    }

                    xBuilder.CloseTag (); // div.files
                }
            }

            iAddAttachedFilesPart (null);

            xBuilder.OpenTag ("div", new [] { "class", "entries" });

            // (long, TaskInfo?, NoteInfo?) も考えたが、1項目1エントリーなので object の方が良さそう
            // ボックス化などを伴わない、LINQ によるスマートな方法を探したが、自分には分からなかった

            List <(long Utc, object Entry)> xEntries = new List <(long Utc, object Entry)> ();

            xEntries.AddRange (MergedTaskList.AllButMemoTasks.
                Where (x => x.State == TaskState.Done || x.State == TaskState.Cancelled).
                Select (y => (y.HandlingUtc!.Value, (object) y)));

            xEntries.AddRange (MergedTaskList.AllMemoNotes.
                Where (x => x.ParentTask!.State == TaskState.Done || x.ParentTask!.State == TaskState.Cancelled).
                Select (y => (y.CreationUtc, (object) y)));

            foreach (object xEntry in xEntries.OrderBy (x => x.Utc).Select (y => y.Entry))
            {
                void iAddNotePart (NoteInfo note)
                {
                    xBuilder.OpenTag ("div", new [] { "class", "note" });

                    xBuilder.OpenTag ("div", new [] { "class", "contents" });

                    // todo

                    xBuilder.CloseTag (); // div.contents

                    iAddAttachedFilesPart (note.Guid);

                    xBuilder.CloseTag (); // div.note
                }

                if (xEntry.GetType () == typeof (TaskInfo))
                {
                    TaskInfo xTask = (TaskInfo) xEntry;

                    xBuilder.OpenTag ("div", new [] { "class", $"task {(xTask.State == TaskState.Done ? "done" : "canceled")}" });

                    // todo

                    iAddAttachedFilesPart (xTask.Guid);

                    if (xTask.Notes.Count > 0)
                    {
                        xBuilder.OpenTag ("notes");

                        foreach (NoteInfo xNote in xTask.Notes)
                            iAddNotePart (xNote);

                        xBuilder.CloseTag (); // div.notes
                    }

                    xBuilder.CloseTag (); // div.task
                }

                else if (xEntry.GetType () == typeof (NoteInfo))
                    iAddNotePart ((NoteInfo) xEntry);
            }

            xBuilder.CloseTag (); // div.entries

            xBuilder.CloseTag (); // body

            xBuilder.CloseTag (); // html

            string xFileContents = xBuilder.ToString ();

            try
            {
                string? xOldFileContents = null;

                if (nFile.Exists (MergedTaskList.Attributes.DestFilePath))
                    xOldFileContents = nFile.ReadAllText (MergedTaskList.Attributes.DestFilePath);

                if (string.Equals (xFileContents, xOldFileContents, StringComparison.Ordinal) == false)
                {
                    nFile.WriteAllText (MergedTaskList.Attributes.DestFilePath, xFileContents);

                    if (xOldFileContents == null)
                        result = iPageGenerationResult.Created;

                    else result = iPageGenerationResult.Updated;
                }

                else result = iPageGenerationResult.Unchanged;

                return true;
            }

            catch
            {
                xErrorMessages.Add ("ファイルの読み書きに失敗しました: " + MergedTaskList.Attributes.DestFilePath);

                result = default;
                return false;
            }
        }
    }
}
