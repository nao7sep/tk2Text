using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace tkView
{
    internal static class iShared
    {
        public static bool IsMainWindowClosed = true;

        private static bool? mIsLogging = null;

        public static bool IsLogging
        {
            get
            {
                if (mIsLogging == null)
                {
                    if ("True".Equals (ConfigurationManager.AppSettings ["IsLogging"], StringComparison.OrdinalIgnoreCase))
                        mIsLogging = true;

                    else mIsLogging = false;
                }

                return mIsLogging.Value;
            }
        }

        // 複数のコードブロックでこの処理が必要になったのでメソッド化
        // MainWindow のフィールドなどへの非依存を示すため、こちらに入れておく

        public static void UpdateListBoxItemSelection (ListBox control, int selectedIndex, bool isItemFocused)
        {
            // 項目が減っていれば、最後が選択されるように
            // その「最後」が負なら空になっているので処理がスキップされる

            int xNewSelectedIndex = selectedIndex;

            if (xNewSelectedIndex >= control.Items.Count)
                xNewSelectedIndex = control.Items.Count - 1;

            if (xNewSelectedIndex >= 0)
            {
                // 古いコメントには、ListBox のレンダリングは仮想化されているため、こうやって実体を生成しないと後続の処理がうまくいかないとある
                // いま調べると、UpdateLayout は、dispatcher に残りの処理を終わらせてクリーンな状態から作業を再開するためのもののようだ

                // c# - Why do I have have to use UIElement.UpdateLayout? - Stack Overflow
                // https://stackoverflow.com/questions/27894477/why-do-i-have-have-to-use-uielement-updatelayout

                control.UpdateLayout ();
                control.ScrollIntoView (control.Items [xNewSelectedIndex]);
                // control.SelectedIndex = xNewSelectedIndex;

                ListBoxItem? xItem = control.ItemContainerGenerator.ContainerFromIndex (xNewSelectedIndex) as ListBoxItem;

                if (xItem != null)
                {
                    xItem.IsSelected = true;

                    if (isItemFocused)
                        xItem.Focus ();
                }
            }
        }

        public static void HandleException (Window owner, Exception exception)
        {
            MessageBox.Show (owner, $"エラーが発生しました:{Environment.NewLine}{Environment.NewLine}{exception}");
        }

        private static TextFormattingMode? mTextFormattingMode = null;

        public static TextFormattingMode TextFormattingMode
        {
            get
            {
                if (mTextFormattingMode == null)
                {
                    if (Enum.TryParse (ConfigurationManager.AppSettings ["TextFormattingMode"], out TextFormattingMode xResult))
                        mTextFormattingMode = xResult;

                    // return new TextFormatterImp(soleContext, TextFormattingMode.Ideal) というコードがある
                    // ほかに決め打ちになっているところはないようなので、Ideal をデフォルト値とみなす

                    // TextFormatter.cs
                    // https://source.dot.net/#PresentationCore/System/Windows/Media/textformatting/TextFormatter.cs

                    // プログラム起動時の値がこうなっていることも確認した

                    else mTextFormattingMode = TextFormattingMode.Ideal;
                }

                return mTextFormattingMode.Value;
            }
        }

        private static TextHintingMode? mTextHintingMode = null;

        public static TextHintingMode TextHintingMode
        {
            get
            {
                if (mTextHintingMode == null)
                {
                    if (Enum.TryParse (ConfigurationManager.AppSettings ["TextHintingMode"], out TextHintingMode xResult))
                        mTextHintingMode = xResult;

                    // プログラム起動時の値がこうなっていることを確認した
                    else mTextHintingMode = TextHintingMode.Auto;
                }

                return mTextHintingMode.Value;
            }
        }

        private static TextRenderingMode? mTextRenderingMode = null;

        public static TextRenderingMode TextRenderingMode
        {
            get
            {
                if (mTextRenderingMode == null)
                {
                    if (Enum.TryParse (ConfigurationManager.AppSettings ["TextRenderingMode"], out TextRenderingMode xResult))
                        mTextRenderingMode = xResult;

                    // プログラム起動時の値がこうなっていることを確認した
                    else mTextRenderingMode = TextRenderingMode.Auto;
                }

                return mTextRenderingMode.Value;
            }
        }
    }
}
