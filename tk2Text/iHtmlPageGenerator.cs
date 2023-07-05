using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
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

        // メアドや URL をリンク化しながら全体を HTML エンコードする
        // さらに、入力がメモなら段落分けして <p> にも入れる

        private static string iHtmlEncode (string value, bool isTask, int? indentationWidth)
        {
            // ChatGPT に聞いた正規表現
            // 詳細については taskKiller のログの方に

            // regex という識別子一つで「パターン」のニュアンスも含みそうだが、
            //     regex だけでは「正規表現の」と形容詞的なところもあるのでこのまま

            const string
                xEmailAddressRegexPattern = @"[a-z0-9._%+-]+@[a-z0-9.-]+\.[a-z]{2,}",
                xUrlRegexPattern = @"(?:https?://|www\.)\S+",
                xSinglePattern = $"(?<EmailAddress>{xEmailAddressRegexPattern})|(?<Url>{xUrlRegexPattern})";

            StringBuilder xBuilder = new StringBuilder ();

            int xProcessedLength = 0;

            foreach (Match xMatch in Regex.Matches (value, xSinglePattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
            {
                xBuilder.Append (WebUtility.HtmlEncode (value.Substring (xProcessedLength, xMatch.Index - xProcessedLength)));

                // Group Class (System.Text.RegularExpressions) | Microsoft Learn
                // https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.group

                // Group.Success Property (System.Text.RegularExpressions) | Microsoft Learn
                // https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.group.success

                bool xIsEmailAddress = xMatch.Groups ["EmailAddress"].Success;

                // taskKiller のメモはブラウザーからのコピペなので、この時点で URL エンコードはできている
                // 試しに、さらに URL エンコードをやってみたところ :// の部分などまでエンコードされ、表示が乱れ、リンクも機能しなかった
                // 一応 HTML エンコードを行うのは、クエリー文字列などに & などが含まれていてもそのまま出力されないための作法

                string xEncodedValue = WebUtility.HtmlEncode (xMatch.Value),
                    xMailToPart = xIsEmailAddress ? "mailto:" : string.Empty,
                    xTargetPart = xIsEmailAddress ? string.Empty : " target=\"_blank\"";

                xBuilder.Append ($"<a href=\"{xMailToPart}{xEncodedValue}\"{xTargetPart}>{xEncodedValue}</a>");

                xProcessedLength = xMatch.Index + xMatch.Length;
            }

            if (value.Length > xProcessedLength)
                xBuilder.Append (WebUtility.HtmlEncode (value.Substring (xProcessedLength)));

            // タスクなら、<p> に入れず、インデントもつけずに返す

            if (isTask)
                return xBuilder.ToString ();

            string xIndentationString = iHtmlStringBuilder.IndentationString.Substring (0, indentationWidth!.Value),
                xWiderIndentationString = iHtmlStringBuilder.IndentationString.Substring (0, indentationWidth!.Value + 4);

            return string.Concat (xBuilder.ToString ().nSplitIntoParagraphs ().Select (x =>
            {
                return xIndentationString + "<p>" +
                    string.Join ($"<br />{Environment.NewLine}{xWiderIndentationString}", x.nSplitIntoLines ()) +
                    "</p>" + Environment.NewLine;
            }));
        }

        public bool TryGenerate (out iPageGenerationResult result)
        {
            List <string> xErrorMessages = (List <string>) ErrorMessages;

            // =============================================================================
            //     添付ファイルを処理
            // =============================================================================

            // 実装がややこしくなるし、たいてい作業ミスだろうから、同じファイルが複数回添付されている場合に対処しない
            // ページを見る人の分かりやすさのために同じファイルや画像を複数のタスクやメモに表示する運用は今のところ考えにくい

            List <iAttachedFileManager> xHandledAttachedFiles = new List <iAttachedFileManager> ();

            foreach (iAttachedFileInfo xAttachedFile in MergedTaskList.AttachedFiles.OrderBy (x => x.AttachedAtUtc))
            {
                for (int temp = 0; ; temp ++)
                {
                    string xAttachedFileDestRelativePath,
                        xAttachedFileDestPath;

                    if (temp == 0)
                    {
                        xAttachedFileDestRelativePath = nPath.Combine (MergedTaskList.Attributes.AttachedFileDirectoryRelativePath, xAttachedFile.File.Name);
                        xAttachedFileDestPath = nPath.Combine (MergedTaskList.Attributes.DestDirectoryPath, xAttachedFileDestRelativePath);
                    }

                    else
                    {
                        xAttachedFileDestRelativePath = nPath.Combine (MergedTaskList.Attributes.AttachedFileDirectoryRelativePath, temp.ToString (CultureInfo.InvariantCulture), xAttachedFile.File.Name);
                        xAttachedFileDestPath = nPath.Combine (MergedTaskList.Attributes.DestDirectoryPath, xAttachedFileDestRelativePath);
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
                                    xHandledAttachedFiles.Add (new iAttachedFileManager (xAttachedFileDestRelativePath, xAttachedFileDestPath, xAttachedFile));
                                    break;
                                }
                            }

                            nDirectory.CreateForFile (xAttachedFileDestPath);
                            xAttachedFile.File.CopyTo (xAttachedFileDestPath, true);
                            nFile.SetLastWriteUtc (xAttachedFileDestPath, xAttachedFile.ModifiedAtUtc);

                            iAttachedFileManager xManager = new iAttachedFileManager (xAttachedFileDestRelativePath, xAttachedFileDestPath, xAttachedFile);

                            if (xManager.IsImage && xManager.IsOptimized)
                                xManager.Resize ();
#if DEBUG
                            if (xFileExisted == false)
                                Console.WriteLine ("Created Attached File: " + xAttachedFileDestPath);

                            else Console.WriteLine ("Updated Attached File: " + xAttachedFileDestPath);
#endif
                            xHandledAttachedFiles.Add (xManager);
                            break;
                        }

                        catch
                        {
                            xErrorMessages.Add ("添付ファイルの読み書きに失敗しました: " + xAttachedFileDestPath);

                            // 続けても仕方ないのでメソッドを抜ける

                            result = default;
                            return false;
                        }
                    }
                }
            }

            if (nDirectory.Exists (MergedTaskList.Attributes.AttachedFileDirectoryPath))
            {
                string [] xUnhandledAttachedFilePaths = Directory.GetFiles (MergedTaskList.Attributes.AttachedFileDirectoryPath, "*.*", SearchOption.AllDirectories).
                    Where (x => xHandledAttachedFiles.All (y => string.Equals (y.DestFilePath, x, StringComparison.OrdinalIgnoreCase) == false &&
                        string.Equals (y.OptimizedImageFilePath, x, StringComparison.OrdinalIgnoreCase) == false)). // 原版と縮小版の両方とパスが不一致
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

            // =============================================================================
            //     CSS ファイルをコピー
            // =============================================================================

            const string xCssFileName = "tk2Text.css";

            FileInfo xSourceCssFile = new FileInfo (nPath.Combine (iShared.AppDirectoryPath, xCssFileName)),
                xDestCssFile = new FileInfo (nPath.Combine (MergedTaskList.Attributes.DestDirectoryPath, xCssFileName));

            if (xDestCssFile.Exists == false ||
                (xSourceCssFile.LastWriteTimeUtc - xDestCssFile.LastWriteTimeUtc).TotalSeconds > 3)
            {
#if DEBUG
                if (xDestCssFile.Exists == false)
                    Console.WriteLine ("Created CSS File: " + xDestCssFile.FullName);

                else Console.WriteLine ("Updated CSS File: " + xDestCssFile.FullName);
#endif
                xSourceCssFile.CopyTo (xDestCssFile.FullName, true);
            }

            else
            {
#if DEBUG
                Console.WriteLine ("Unchanged CSS File: " + xDestCssFile.FullName);
#endif
            }

            // =============================================================================
            //     HTML を生成
            // =============================================================================

            iHtmlStringBuilder xBuilder = new iHtmlStringBuilder ();

            xBuilder.Append ($"<!DOCTYPE html>{Environment.NewLine}");

            xBuilder.OpenTag ("html");

            xBuilder.OpenTag ("head");
            xBuilder.AddTag ("title", safeValue: WebUtility.HtmlEncode (MergedTaskList.Attributes.Title));
            xBuilder.AddTag ("meta", new [] { "name", "viewport", "content", "width=device-width, initial-scale=1" });
            xBuilder.AddTag ("link", new [] { "href", "tk2Text.css", "rel", "stylesheet" });
            xBuilder.CloseTag (); // head

            xBuilder.OpenTag ("body");

            xBuilder.OpenTag ("div", new [] { "class", "title" });
            xBuilder.AddTag ("a", new [] { "href", WebUtility.HtmlEncode (MergedTaskList.Attributes.DestFileName), "class", "title" }, WebUtility.HtmlEncode (MergedTaskList.Attributes.Title));
            xBuilder.CloseTag (); // div.title

            void iAddAttachedFilesPart (Guid? parentGuid)
            {
                var xAttachedFiles = xHandledAttachedFiles.Where (x => x.File.ParentGuid == parentGuid).ToArray ();

                if (xAttachedFiles.Length > 0)
                {
                    xBuilder.OpenTag ("div", new [] { "class", "files" });

                    foreach (var xAttachedFile in xAttachedFiles)
                    {
                        xBuilder.OpenTag ("div", new [] { "class", "file" });

                        if (xAttachedFile.IsImage == false)
                            xBuilder.AddTag ("a", new [] { "href", WebUtility.HtmlEncode (xAttachedFile.DestRelativeFilePath!), "target", "_blank", "class", "file" }, WebUtility.HtmlEncode (xAttachedFile.File.File.Name));

                        else
                        {
                            if (xAttachedFile.IsOptimized == false)
                                xBuilder.AddTag ("img", new [] { "src", WebUtility.HtmlEncode (xAttachedFile.DestRelativeFilePath!), "class", "image" });

                            else
                            {
                                xBuilder.OpenTag ("a", new [] { "href", WebUtility.HtmlEncode (xAttachedFile.DestRelativeFilePath), "target", "_blank", "class", "image" });
                                xBuilder.AddTag ("img", new [] { "src", WebUtility.HtmlEncode (xAttachedFile.OptimizedImageRelativeFilePath!), "class", "image" });
                                xBuilder.CloseTag (); // a.image
                            }
                        }

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

                    xBuilder.Append (iHtmlEncode (note.Contents!, false, xBuilder.IndentationWidth));

                    xBuilder.CloseTag (); // div.contents

                    iAddAttachedFilesPart (note.Guid);

                    xBuilder.CloseTag (); // div.note
                }

                if (xEntry.GetType () == typeof (TaskInfo))
                {
                    TaskInfo xTask = (TaskInfo) xEntry;
                    string xGuidString = xTask.Guid.ToString ("D");

                    xBuilder.OpenTag ("div", new [] { "id", xGuidString, "class", $"task {(xTask.State == TaskState.Done ? "done" : "canceled")}" });

                    string xFirstPart = xTask.State == TaskState.Done ? "&check;" : "&cross;",
                        xLastPart = $"<a href=\"#{xGuidString}\" class=\"permalink\">&infin;</a>";

                    // 今のところ <b> で足りるが、HTML 文書構造の各部には装飾でなく「意味」をつけていきたい
                    // それなら CSS に装飾を丸投げでき、構造と装飾がゴチャゴチャに混ざらない

                    // <b>: The Bring Attention To element - HTML: HyperText Markup Language | MDN
                    // https://developer.mozilla.org/en-US/docs/Web/HTML/Element/b

                    xBuilder.AddTag ("div", new [] { "class", "contents" }, $"{xFirstPart}<span class=\"contents\">{iHtmlEncode (xTask.Contents!, true, null)}</span>{xLastPart}");

                    iAddAttachedFilesPart (xTask.Guid);

                    if (xTask.Notes.Count > 0)
                    {
                        xBuilder.OpenTag ("div", new [] { "class", "notes" });

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

            // =============================================================================
            //     ファイルを読み書き
            // =============================================================================

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
