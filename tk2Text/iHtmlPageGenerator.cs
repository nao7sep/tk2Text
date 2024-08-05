using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Markdig;
using Nekote;

namespace tk2Text
{
    internal class iHtmlPageGenerator
    {
        public readonly iParametersStringParser Parser;

        public readonly iStringReplacer Replacer;

        public readonly iMergedTaskListInfo MergedTaskList;

        public IEnumerable <string> ErrorMessages;

        public iHtmlPageGenerator (iParametersStringParser parser, iStringReplacer replacer, iMergedTaskListInfo mergedTaskList)
        {
            Parser = parser;
            Replacer = replacer;
            MergedTaskList = mergedTaskList;
            ErrorMessages = new List <string> ();
        }

        // メアドや URL をリンク化しながら全体を HTML エンコードする
        // さらに、入力がメモなら段落分けして <p> にも入れる

        private static string iHtmlEncode (string value, bool isTask, int? indentationWidth)
        {
            static string iHandleEmailAddressesAndUrls (string value)
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

                foreach (Match xMatch in Regex.Matches (value, xSinglePattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase).Cast <Match> ())
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

                    // クラス名を短く
                    xBuilder.Append ($"<a href=\"{xMailToPart}{xEncodedValue}\"{xTargetPart} class=\"url\">{xEncodedValue}</a>");

                    xProcessedLength = xMatch.Index + xMatch.Length;
                }

                if (value.Length > xProcessedLength)
                    xBuilder.Append (WebUtility.HtmlEncode (value.Substring (xProcessedLength)));

                return xBuilder.ToString ();
            }

            // タスクなら、<p> に入れず、インデントもつけずに返す

            if (isTask)
                return iHandleEmailAddressesAndUrls (value);

            // メモなら、段落分けし、各段落の先頭や末尾の @ を検出し、AI の回答の引用の部分を検出し、そうでない部分と区別して処理

            string [] xParagraphs = value.nSplitIntoParagraphs ();
            List <(bool IsAiGenerated, string Value)> xParts = new List <(bool IsAiGenerated, string Value)> ();

            for (int temp = 0; temp < xParagraphs.Length; temp ++)
            {
                if (xParagraphs [temp].StartsWith ("@") == false)
                    xParts.Add ((false, xParagraphs [temp]));

                else
                {
                    int xEndOfAiGeneratedPart = -1;

                    // temp + 1 から見ていたが、1段落だけの回答もある

                    for (int tempAlt = temp; tempAlt < xParagraphs.Length; tempAlt ++)
                    {
                        if (xParagraphs [tempAlt].EndsWith ("@"))
                        {
                            xEndOfAiGeneratedPart = tempAlt;
                            break;
                        }
                    }

                    if (xEndOfAiGeneratedPart < 0)
                        xEndOfAiGeneratedPart = xParagraphs.Length - 1;

                    xParts.Add ((true, string.Join (Environment.NewLine + Environment.NewLine, xParagraphs [temp .. (xEndOfAiGeneratedPart + 1)]).Trim ('@', '\x20')));
                    temp = xEndOfAiGeneratedPart; // 直後に ++ されるので +1 は不要
                }
            }

            string xIndentationString = iHtmlStringBuilder.IndentationString.Substring (0, indentationWidth!.Value),
                xWiderIndentationString = iHtmlStringBuilder.IndentationString.Substring (0, indentationWidth!.Value + 4);

            return string.Concat (xParts.Select (x =>
            {
                if (x.IsAiGenerated)
                {
                    if (iShared.ExcludesAiMessages)
                        return string.Empty;

                    return xIndentationString + "<div class=\"note_ai_generated\">" + Environment.NewLine +
                        Markdown.ToHtml (x.Value).TrimEnd () + Environment.NewLine +
                        xIndentationString + "</div>" + Environment.NewLine;
                }

                else return xIndentationString + "<p class=\"note_contents\">" +
                    string.Join ($"<br />{Environment.NewLine}{xWiderIndentationString}",
                    iHandleEmailAddressesAndUrls (x.Value).nSplitIntoLines ().Select (y => iShared.ReplaceIndentationChars (y))) +
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

            var xAllMemoTaskGuids = MergedTaskList.AllMemoNotes.Select (x => x.ParentTask!.Guid).Distinct ();
            var xFilesAttachedToMemoTasks = MergedTaskList.AttachedFiles.Where (x => x.ParentGuid != null && xAllMemoTaskGuids.Contains (x.ParentGuid.Value)).ToArray ();

            if (xFilesAttachedToMemoTasks.Length > 0)
            {
                xErrorMessages.AddRange (xFilesAttachedToMemoTasks.Select (x => $"「メモ」タスクにファイルが添付されています: {x.File.FullName}"));

                // そのままページを生成しても処理において特に問題がないが、
                //     すぐに対処してもらわないとユーザーが忘れうるので、いったん打ち切る

                result = default;
                return false;
            }

            List <iAttachedFileManager> xHandledAttachedFiles = new List <iAttachedFileManager> ();

            // 追記: 派生開発により少し無駄のあるコードになったが、未処理のタスクやメモに添付されているファイルが出力先にコピーされないようにした
            // あとで読もうと添付した PDF ファイルが、タスクの方がまだなのに出力先に全てコピーされるのは、データの不整合にほかならない

            List <Guid> xAllHandledParentGuids = new List <Guid> ();
            IEnumerable <TaskInfo> xAllHandledTasks;

            // 「ストリーミング」モードなら未処理のタスクもログとして出力される

            if (MergedTaskList.Attributes.IsStreaming)
                xAllHandledTasks = MergedTaskList.AllButMemoTasks;

            else xAllHandledTasks = MergedTaskList.AllButMemoTasks.Where (x => x.State == TaskState.Done || x.State == TaskState.Cancelled);

            xAllHandledParentGuids.AddRange (xAllHandledTasks.Select (y => y.Guid));
            xAllHandledParentGuids.AddRange (xAllHandledTasks.SelectMany (x => x.Notes).Select (y => y.Guid));
            xAllHandledParentGuids.AddRange (MergedTaskList.AllMemoNotes.Where (x =>
            {
                if (MergedTaskList.Attributes.IsStreaming)
                    return true;

                else return x.ParentTask!.State == TaskState.Done || x.ParentTask!.State == TaskState.Cancelled;
            }).
            Select (y => y.Guid));

            // タスクリストそのものに添付されているファイルは、ParentGuid が null になる
            // ここでソートするのは、ファイル名が衝突したら連番が入る仕組みにおいて、できるだけパスの同一性が保たれるようにするため

            foreach (iAttachedFileInfo xAttachedFile in MergedTaskList.AttachedFiles.Where (x => x.ParentGuid == null || xAllHandledParentGuids.Contains (x.ParentGuid.Value)).OrderBy (y => y.AttachedAtUtc))
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
                                    xHandledAttachedFiles.Add (new iAttachedFileManager (xAttachedFileDestRelativePath, xAttachedFileDestPath, xAttachedFile, MergedTaskList.Attributes.IsStreaming));
                                    break;
                                }
                            }

                            nDirectory.CreateForFile (xAttachedFileDestPath);
                            xAttachedFile.File.CopyTo (xAttachedFileDestPath, true);
                            nFile.SetLastWriteUtc (xAttachedFileDestPath, xAttachedFile.ModifiedAtUtc);

                            iAttachedFileManager xManager = new iAttachedFileManager (xAttachedFileDestRelativePath, xAttachedFileDestPath, xAttachedFile, MergedTaskList.Attributes.IsStreaming);

                            if (xManager.IsImage && xManager.IsResized)
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
                        string.Equals (y.ResizedImageFilePath, x, StringComparison.OrdinalIgnoreCase) == false)). // 原版と縮小版の両方とパスが不一致
                    OrderBy (z => z, StringComparer.OrdinalIgnoreCase). // 一応、ファイルパスでソート
                    ToArray (); // LINQ が複数回処理されるのを回避

                if (xUnhandledAttachedFilePaths.Length > 0)
                {
                    xErrorMessages.AddRange (xUnhandledAttachedFilePaths.Select (x => $"古い添付ファイルが残っています: {x}"));

                    // そのままページを生成しても処理において特に問題がないが、
                    //     すぐに対処してもらわないとユーザーが忘れうるので、いったん打ち切る

                    result = default;
                    return false;
                }
            }

            // =============================================================================
            //     CSS ファイルをコピー → 不要
            // =============================================================================

            /* const string xCssFileName = "tk2Text.css";

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
            } */

            // =============================================================================
            //     HTML を生成
            // =============================================================================

            iHtmlStringBuilder xBuilder = new iHtmlStringBuilder ();

            xBuilder.Append ($"<!DOCTYPE html>{Environment.NewLine}");

            xBuilder.OpenTag ("html");

            xBuilder.OpenTag ("head");
            xBuilder.AddTag ("title", safeValue: WebUtility.HtmlEncode (MergedTaskList.Attributes.Title));
            xBuilder.AddTag ("meta", new [] { "name", "viewport", "content", "width=device-width, initial-scale=1" });
            xBuilder.AddTag ("style", safeValue: iShared.MinifiedCssString);
            xBuilder.CloseTag ();

            xBuilder.OpenTag ("body");

            xBuilder.OpenTag ("div", new [] { "class", "title" });
            xBuilder.AddTag ("a", new [] { "href", WebUtility.HtmlEncode (MergedTaskList.Attributes.DestFileName), "class", "title" }, WebUtility.HtmlEncode (MergedTaskList.Attributes.Title));
            xBuilder.CloseTag ();

            void iAddAttachedFilesPart (Guid? parentGuid)
            {
                var xAttachedFiles = xHandledAttachedFiles.Where (x => x.File.ParentGuid == parentGuid).ToArray ();

                if (xAttachedFiles.Length > 0)
                {
                    xBuilder.OpenTag ("div", new [] { "class", "attached" });

                    void iAddAttachedImagePart (iAttachedFileManager file)
                    {
                        xBuilder.OpenTag ("div", new [] { "class", "image" });

                        if (file.IsResized == false)
                            xBuilder.AddTag ("img", new [] { "src", iShared.ToUnixDirectorySeparators (WebUtility.HtmlEncode (file.DestRelativeFilePath!)), "class", "image" });

                        else
                        {
                            xBuilder.OpenTag ("a", new [] { "href", iShared.ToUnixDirectorySeparators (WebUtility.HtmlEncode (file.DestRelativeFilePath)), "target", "_blank", "class", "image" });
                            xBuilder.AddTag ("img", new [] { "src", iShared.ToUnixDirectorySeparators (WebUtility.HtmlEncode (file.ResizedImageRelativeFilePath!)), "class", "image" });
                            xBuilder.CloseTag ();
                        }

                        xBuilder.CloseTag ();
                    }

                    void iAddAttachedFilePart (iAttachedFileManager file, bool isStreaming)
                    {
                        xBuilder.OpenTag (isStreaming ? "li" : "div", new [] { "class", "file" });
                        xBuilder.AddTag ("a", new [] { "href", WebUtility.HtmlEncode (iShared.ToUnixDirectorySeparators (file.DestRelativeFilePath!)), "target", "_blank", "class", "file" }, WebUtility.HtmlEncode (file.File.File.Name));
                        xBuilder.CloseTag ();
                    }

                    if (MergedTaskList.Attributes.IsStreaming == false)
                    {
                        foreach (var xAttachedFile in xAttachedFiles.OrderBy (x => x.File.AttachedAtUtc))
                        {
                            if (xAttachedFile.IsImage)
                                iAddAttachedImagePart (xAttachedFile);

                            else iAddAttachedFilePart (xAttachedFile, isStreaming: false);
                        }
                    }

                    else
                    {
                        var xAttachedImages = xAttachedFiles.Where (x => x.IsImage).OrderBy (y => y.File.AttachedAtUtc);
                        var xAttachedFilesAlt = xAttachedFiles.Where (x => x.IsImage == false).OrderBy (y => y.File.AttachedAtUtc);

                        if (xAttachedImages.Any ())
                        {
                            xBuilder.OpenTag ("div", new [] { "class", "images" });

                            foreach (var xAttachedImage in xAttachedImages)
                                iAddAttachedImagePart (xAttachedImage);

                            xBuilder.CloseTag ();
                        }

                        if (xAttachedFilesAlt.Any ())
                        {
                            xBuilder.OpenTag ("ul", new [] { "class", "files" });

                            foreach (var xAttachedFile in xAttachedFilesAlt)
                                iAddAttachedFilePart (xAttachedFile, isStreaming: true);

                            xBuilder.CloseTag ();
                        }
                    }

                    xBuilder.CloseTag ();
                }
            }

            iAddAttachedFilesPart (null);

            xBuilder.OpenTag ("div", new [] { "class", "entries" });

            // (long, TaskInfo?, NoteInfo?) も考えたが、1項目1エントリーなので object の方が良さそう
            // ボックス化などを伴わない、LINQ によるスマートな方法を探したが、自分には分からなかった

            List <(long Utc, object Entry)> xEntries = new List <(long Utc, object Entry)> ();

            xEntries.AddRange (MergedTaskList.AllButMemoTasks.
                Where (x =>
                {
                    if (MergedTaskList.Attributes.IsStreaming)
                        return true;

                    else return x.State == TaskState.Done || x.State == TaskState.Cancelled;
                }).
                Select (y =>
                {
                    if (MergedTaskList.Attributes.IsStreaming)
                        return (y.CreationUtc, (object) y);

                    else return (y.HandlingUtc!.Value, (object) y);
                }));

            xEntries.AddRange (MergedTaskList.AllMemoNotes.
                Where (x =>
                {
                    if (MergedTaskList.Attributes.IsStreaming)
                        return true;

                    else return x.ParentTask!.State == TaskState.Done || x.ParentTask!.State == TaskState.Cancelled;
                }).
                Select (y => (y.CreationUtc, (object) y)));

            foreach (object xEntry in xEntries.OrderBy (x => x.Utc).Select (y => y.Entry))
            {
                void iAddNotePart (NoteInfo note, bool writesGuid)
                {
                    xBuilder.OpenTag ("div", new [] { "class", "note" });

                    xBuilder.OpenTag ("div", new [] { "class", "note_contents" });

                    xBuilder.Append (iHtmlEncode (Replacer.ReplaceAll (note.Contents!), false, xBuilder.IndentationWidth));

                    if (writesGuid)
                        xBuilder.Append ($"{iHtmlStringBuilder.IndentationString.AsSpan (0, xBuilder.IndentationWidth)}<!-- Task: {note.ParentTask!.Guid.ToString ("D")} -->{Environment.NewLine}");

                    xBuilder.CloseTag ();

                    iAddAttachedFilesPart (note.Guid);

                    xBuilder.CloseTag ();
                }

                if (xEntry.GetType () == typeof (TaskInfo))
                {
                    TaskInfo xTask = (TaskInfo) xEntry;
                    string xGuidString = xTask.Guid.ToString ("D");

                    string xClass,
                        xSymbol;

                    if (MergedTaskList.Attributes.IsStreaming)
                    {
                        xClass = "task streaming";
                        xSymbol = string.Empty;
                    }

                    else
                    {
                        if (xTask.State == TaskState.Done)
                        {
                            xClass = "task done";
                            xSymbol = "&check;";
                        }

                        else
                        {
                            xClass = "task canceled";
                            xSymbol = "&cross;";
                        }
                    }

                    xBuilder.OpenTag ("div", new [] { "id", xGuidString, "class", xClass });

                    string xFirstPart = xSymbol,
                        xLastPart = $"<a href=\"#{xGuidString}\" class=\"permalink\">&infin;</a>";

                    // 今のところ <b> で足りるが、HTML 文書構造の各部には装飾でなく「意味」をつけていきたい
                    // それなら CSS に装飾を丸投げでき、構造と装飾がゴチャゴチャに混ざらない

                    // <b>: The Bring Attention To element - HTML: HyperText Markup Language | MDN
                    // https://developer.mozilla.org/en-US/docs/Web/HTML/Element/b

                    // 単一行として出力するつもりだったものにあとから改行とインデントを入れた
                    // span 部分以外は不変なので diff を取るなどにおいて不都合はそれほどないが、
                    //     HTML をザッと見るにおいてここだけ1行1要素になっていなかったので作法として直した

                    static string iGetNewLineAndIndentationString (int indentationWidth)
                    {
                        return $"{Environment.NewLine}{iHtmlStringBuilder.IndentationString.AsSpan (0, indentationWidth)}";
                    }

                    xBuilder.AddTag ("div", new [] { "class", "task_contents" }, $"{xFirstPart}{iGetNewLineAndIndentationString (xBuilder.IndentationWidth + 4)}<span class=\"task_contents\">{iHtmlEncode (Replacer.ReplaceAll (xTask.Contents!), true, null)}</span>{iGetNewLineAndIndentationString (xBuilder.IndentationWidth + 4)}{xLastPart}{iGetNewLineAndIndentationString (xBuilder.IndentationWidth)}");

                    iAddAttachedFilesPart (xTask.Guid);

                    if (xTask.Notes.Count > 0)
                    {
                        xBuilder.OpenTag ("div", new [] { "class", "notes" });

                        foreach (NoteInfo xNote in xTask.Notes.OrderBy (x => x.CreationUtc))
                            iAddNotePart (xNote, writesGuid: false);

                        xBuilder.CloseTag ();
                    }

                    xBuilder.CloseTag ();
                }

                else if (xEntry.GetType () == typeof (NoteInfo))
                    iAddNotePart ((NoteInfo) xEntry, writesGuid: true);
            }

            xBuilder.CloseTag ();

            xBuilder.CloseTag ();

            xBuilder.CloseTag ();

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
