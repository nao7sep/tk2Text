using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

            List <string> xHandledAttachedFilePaths = new List <string> ();

            foreach (iAttachedFileInfo xAttachedFile in MergedTaskList.AttachedFiles.OrderBy (x => x.AttachedAtUtc))
            {
                for (int temp = 0; ; temp ++)
                {
                    string xAttachedFileDestPath;

                    if (temp == 0)
                        xAttachedFileDestPath = nPath.Combine (MergedTaskList.Attributes.AttachedFileDirectoryPath,
                            xAttachedFile.File.Name);

                    else xAttachedFileDestPath = nPath.Combine (MergedTaskList.Attributes.AttachedFileDirectoryPath,
                        temp.ToString (CultureInfo.InvariantCulture), xAttachedFile.File.Name);

                    if (xHandledAttachedFilePaths.Contains (xAttachedFileDestPath, StringComparer.OrdinalIgnoreCase) == false)
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
                            xHandledAttachedFilePaths.Add (xAttachedFileDestPath);
                        }
                    }
                }
            }

            if (nDirectory.Exists (MergedTaskList.Attributes.AttachedFileDirectoryPath))
            {
                string [] xUnhandledAttachedFiles = Directory.GetFiles (MergedTaskList.Attributes.AttachedFileDirectoryPath, "*.*", SearchOption.AllDirectories).
                    Where (x => xHandledAttachedFilePaths.Contains (x, StringComparer.OrdinalIgnoreCase) == false).
                    ToArray (); // LINQ が複数回処理されるのを回避

                if (xUnhandledAttachedFiles.Length > 0)
                {
                    xErrorMessages.AddRange (xUnhandledAttachedFiles.Select (x => $"古い添付ファイルが残っています: {x}"));

                    // こちらは、そのままページを生成しても処理において特に問題がないが、
                    //     すぐに対処してもらわないとユーザーが忘れうるので、いったん打ち切る

                    result = default;
                    return false;
                }
            }

            StringBuilder xBuilder = new StringBuilder ();

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
                if (xEntry.GetType () == typeof (TaskInfo))
                {
                    TaskInfo xTask = (TaskInfo) xEntry;
                }

                else if (xEntry.GetType () == typeof (NoteInfo))
                {
                    NoteInfo xNote = (NoteInfo) xEntry;
                }
            }

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
