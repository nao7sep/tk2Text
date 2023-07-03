using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tk2Text
{
    internal class iHtmlStringBuilder
    {
        public readonly StringBuilder Builder;

        // 現状、7～8段と思うので、まだまだ余裕がある
        public static readonly string IndentationString = new string ('\x20', 64);

        public int IndentationWidth;

        public readonly Stack <string> Tags;

        // Empty HTML Tags (21 Weird Things You Need To Know!)
        // https://matthewjamestaylor.com/empty-tags

        public static readonly IEnumerable <string> EmptyTags = new [] { "area", "base", "br", "col", "embed", "hr", "img", "input", "keygen", "link", "meta", "param", "source", "track", "wbr" };

        public iHtmlStringBuilder ()
        {
            Builder = new StringBuilder ();
            IndentationWidth = 0;
            Tags = new Stack <string> ();
        }

        private void iAddIndentation ()
        {
            if (IndentationWidth == 0)
                return;

            Builder.Append (IndentationString.AsSpan (0, IndentationWidth));
        }

        private void iOpenTag (string safeName, IEnumerable <string>? safeAttributes)
        {
            Tags.Push (safeName);

            string xAttributesString = string.Empty;

            if (safeAttributes != null && safeAttributes.Count () >= 2)
                xAttributesString = string.Concat (Enumerable.Range (0, safeAttributes.Count () / 2).
                    Select (x => $" {safeAttributes.ElementAt (x << 1)}=\"{safeAttributes.ElementAt ((x << 1) + 1)}\""));

            Builder.Append ($"<{Tags.Peek ()}{xAttributesString}>");
        }

        public iHtmlStringBuilder OpenTag (string safeName, IEnumerable <string>? safeAttributes = null)
        {
            iAddIndentation ();
            IndentationWidth += 4;

            iOpenTag (safeName, safeAttributes);

            Builder.AppendLine ();

            return this;
        }

        private void iCloseTag ()
        {
            // やや強引だが、値のないタグなら終了タグが省略されることもあるように
            // こうすることで AddTag で img などをうまく出力できる

            if (Builder.Length > 0 && Builder [Builder.Length - 1] == '>')
            {
                if (EmptyTags.Contains (Tags.Peek (), StringComparer.OrdinalIgnoreCase))
                {
                    Tags.Pop ();

                    Builder.Length -= 1;
                    Builder.Append (" />");

                    return;
                }
            }

            Builder.Append ($"</{Tags.Pop ()}>");
        }

        public iHtmlStringBuilder CloseTag ()
        {
            IndentationWidth -= 4;
            iAddIndentation ();

            iCloseTag ();

            Builder.AppendLine ();

            return this;
        }

        public iHtmlStringBuilder AddTag (string safeName, IEnumerable <string>? safeAttributes = null, string? safeValue = null)
        {
            iAddIndentation ();

            iOpenTag (safeName, safeAttributes);

            Builder.Append (safeValue);

            iCloseTag ();

            Builder.AppendLine ();

            return this;
        }

        public override string ToString ()
        {
            return Builder.ToString ();
        }
    }
}
