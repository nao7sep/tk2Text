using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

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
    }
}
