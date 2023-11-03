using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
// using System.Windows.Shapes;
using tk2Text;

namespace tkView
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow: Window
    {
        public MainWindow ()
        {
            InitializeComponent ();

            // リロード時のほかのアプリのカクつきを低減できるか
            Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
        }

        // こういうデータは、特定のウィンドウのコードでなく、アプリ全体からアクセスできるところに定義することが多い
        // しかし、tkView は完全に自分しか使わないものなので、うるさくならない範囲内でベタ書きにして手抜き

        private readonly string mParametersFilePath = Path.Join (Path.GetDirectoryName (Assembly.GetExecutingAssembly ().Location), "Parameters.txt");

        private string []? mTaskKillerDirectoryPaths = null;

        private string [] taskKillerDirectoryPaths
        {
            get
            {
                if (mTaskKillerDirectoryPaths == null)
                {
                    List <string> xDirectoryPaths = new List <string> ();

                    if (File.Exists (mParametersFilePath))
                    {
                        List <string>
                            xIncludedDirectoryPaths = new List <string> (),
                            xExcludedDirectoryPaths = new List <string> ();

                        foreach (string xLine in File.ReadAllLines (mParametersFilePath, Encoding.UTF8))
                        {
                            string xTrimmed = xLine.Trim ();

                            if (xTrimmed.Length > 0)
                            {
                                if (xTrimmed.Contains (':') == false)
                                {
                                    MessageBox.Show (this, $"パラメーターが不正です: {xLine}");
                                    continue;
                                }

                                int xIndex = xTrimmed.IndexOf (':');

                                string xKey = xTrimmed.Substring (0, xIndex).TrimEnd (),
                                    xValue = xTrimmed.Substring (xIndex + 1).TrimStart ();

                                if (Path.IsPathFullyQualified (xValue) == false || Directory.Exists (xValue) == false)
                                {
                                    MessageBox.Show (this, $"パラメーターの値が不正です: {xValue}");
                                    continue;
                                }

                                if (xKey.Equals ("Include", StringComparison.OrdinalIgnoreCase))
                                    xIncludedDirectoryPaths.Add (xValue);

                                else if (xKey.Equals ("Exclude", StringComparison.OrdinalIgnoreCase))
                                    xExcludedDirectoryPaths.Add (xValue);

                                else MessageBox.Show (this, $"パラメーターのキーが不正です: {xKey}");
                            }
                        }

                        xDirectoryPaths.AddRange (xIncludedDirectoryPaths.SelectMany (x =>
                        {
                            List <string> xDirectoryPaths = Directory.GetDirectories (x, "*.*", SearchOption.AllDirectories).ToList ();
                            xDirectoryPaths.Add (x); // ピンポイントでの指定もできるように

                            return xDirectoryPaths.Where (y =>
                            {
                                // 全てのタスクが処理済みになり、その taskKiller が閉じられれば、Ordering ディレクトリーが消える
                                // 処理したてホヤホヤのタスクが入っていることもあるが、処理の高速化のため無視する
                                // 処理済みタスクは、「こういうことをやってきたから、次はこういうことだ」を考えるためのコンテキスト情報として表示される
                                // 一連のタスクが全て終わったなら、そのタスクリスト関連ではほかにやることがなく、コンテキスト情報は不要

                                // 追記: なぜか iReload で Ordering ディレクトリーなどを見ていたので修正
                                // 実行ファイルがあれば、taskKiller のディレクトリーだと判断される
                                // 除外されておらず、Completed.txt もなければ、処理に入る
                                // データがなかったり、Ordering がなかったりなら、ロードの対象にならない

                                return File.Exists (Path.Join (y, "taskKiller.exe")) &&
                                    xExcludedDirectoryPaths.Contains (y, StringComparer.OrdinalIgnoreCase) == false &&
                                    File.Exists (Path.Join (y, "Completed.txt")) == false &&
                                    Directory.Exists (Path.Join (y, "Tasks")) &&
                                    Directory.Exists (Path.Join (y, "Ordering"));
                            });
                        }).
                        Distinct (StringComparer.OrdinalIgnoreCase));
                    }

                    else File.WriteAllText (mParametersFilePath, string.Empty, Encoding.ASCII);

                    mTaskKillerDirectoryPaths = xDirectoryPaths.ToArray ();
                }

                return mTaskKillerDirectoryPaths;
            }
        }

        // バインディングは相手がプロパティーでないといけない仕組みになっているようだが、ItemsSource ならフィールドでもいけそう
        // フィールドにしてみて、しばらく様子見

        // .net - Why does WPF support binding to properties of an object, but not fields? - Stack Overflow
        // https://stackoverflow.com/questions/842575/why-does-wpf-support-binding-to-properties-of-an-object-but-not-fields

        private readonly ObservableCollection <TaskInfo>
            mTasksToBeHandledSoon = new ObservableCollection <TaskInfo> (),
            mTasksToBeHandledNow = new ObservableCollection <TaskInfo> (),
            mHandledTasks = new ObservableCollection <TaskInfo> ();

        // 起こりうるコリジョンは、ユーザーがボタンを押してのリロードの最中に別スレッドがリロードを始めることと、
        //     別スレッドが始めたのが終わらないうちにユーザーがボタンを押すことの2パターン
        // リロード中のフラグを用意し、前者では次回を待ち、後者ではボタンが押されなかったものと見なす → lock に変更

        private readonly object mReloadingLocker = new object ();

        private readonly string mLogFilePath = Path.Join (Path.GetDirectoryName (Assembly.GetExecutingAssembly ().Location), "tkView.log");

        private void iReload (bool reloadsParametersToo)
        {
            // lock 系のことをベタ書きするにも作法があるようだ
            // if (Monitor.TryEnter (obj)) 内で try/finally のイメージだったが、
            //     それだと確かに try に入る直前にスレッドが abort された場合に finally にも入らず、
            //     Monitor.Exit が呼ばれない

            // c# - Monitor vs lock - Stack Overflow
            // https://stackoverflow.com/questions/4978850/monitor-vs-lock

            bool xLockTaken = false;

            try
            {
                // lock できなければ、すぐにリロードをあきらめる
                // 呼び出し側では、lock を考えず、呼びっぱなしでいい

                Monitor.TryEnter (mReloadingLocker, ref xLockTaken);

                if (xLockTaken == false)
                    return;

                Stopwatch xStopwatch = Stopwatch.StartNew ();

                // null だと初期化される taskKillerDirectoryPaths でなく、m* の方で、「以前あったが、もうないディレクトリー」を探す
                // ディレクトリーが減っていれば、ログの from * lists の部分のことなども考え、ディレクトリーのリストを更新

                if (reloadsParametersToo ||
                        (mTaskKillerDirectoryPaths != null && mTaskKillerDirectoryPaths.Any (x => Directory.Exists (x) == false)))
                    mTaskKillerDirectoryPaths = null;

                List <TaskInfo> xAllTasks = new List <TaskInfo> ();

                foreach (string xDirectoryPath in taskKillerDirectoryPaths)
                {
                    var xSettings = Shared.LoadSettings (xDirectoryPath);

                    string xTaskListTitle;

                    if (xSettings != null && xSettings.ContainsKey ("Title"))
                        xTaskListTitle = xSettings ["Title"];

                    else xTaskListTitle = Path.GetFileName (xDirectoryPath);

                    StateManager xStateManager = new StateManager (xDirectoryPath);
                    OrderManager xOrderManager = new OrderManager (xDirectoryPath);

                    foreach (string xFilePath in Directory.GetFiles (Path.Join (xDirectoryPath, "Tasks"), "*.txt", SearchOption.TopDirectoryOnly))
                    {
                        try
                        {
                            TaskInfo xTask = Shared.LoadTask (xFilePath, xStateManager, xOrderManager);
                            xTask.TaskListDirectoryPath = xDirectoryPath;
                            xTask.TaskListTitle = xTaskListTitle;
                            xAllTasks.Add (xTask);
                        }

                        catch
                        {
                            // おかしいファイルは、このアプリでは無視される
                            // ほかのアプリでチェックされる
                        }
                    }
                }

                // 三つの ListBox をいったん空にして中身を入れ直すにおいて、直前のステートが失われないように

                // ListBox と ListBoxItem の両方に IsFocused と IsKeyboardFocused がある
                // たとえば項目をクリックするだけでは色だけついて、キーボードの上下キーで前後を選択すれば点線の枠線が表示される
                // となると、クリックだけでは前者のみ true で、キーボード操作により両方とも true になることが想定されるが、
                //     When a control receives logical focus, WPF attempts to give it keyboard focus as well ともある
                // いずれによるフォーカスであっても、IsFocused の方は true になりそうなので、以下、それだけを見ている

                // c# - Why does WPF IsKeyboardFocused provide false information? - Stack Overflow
                // https://stackoverflow.com/questions/48157550/why-does-wpf-iskeyboardfocused-provide-false-information

                // ListBox のめんどくさいところは、入れ物にフォーカスがあるのか、中身にフォーカスがあるのかが別のところ
                // タブキーで ListBox にフォーカスを移すと、まずは入れ物の方にフォーカスがある状態になる
                // そこから下キーを押すと、入れ物から中身の方にフォーカスが移り、一つ目の項目から順にフォーカスが当たる

                // 理屈としては、ある入れ物にフォーカスがあれば、ほかのどの入れ物も、どの入れ物の中身も、フォーカスを持たない
                // また、ある項目にフォーカスがあれば、どの入れ物も、ほかのどの項目も、フォーカスを持たない
                // そのため、それぞれの ListBox について、入れ物にフォーカスがあるかどうかと、項目のうちいずれかにフォーカスがあるかどうかを調べれば、
                //     ウィンドウ内の全ての ListBox およびそれらの中身について、フォーカスの状態を戻せる

                // もう一つめんどくさいのは、Selected と Focused が別であること
                // 選択されていてもフォーカスがない場合があるので、SelectedIndex のところに無条件でフォーカスを当てることはできない
                // 理屈としては、フォーカスされている項目は、必ず、選択されている項目でもあると考えられる
                // そのため、SelectedIndex とは別に「その ListBox の項目のうちいずれかにフォーカスがあるのか」を取得

                // 大昔に、C# では識別子に日本語を使えると読んだので試してみた
                // コンパイルでき、IntelliSense などでも問題なしだが、積極的にやる利益はない

                (bool IsControlFocused, int SelectedIndex, bool IsItemFocused) iGetいろいろ (ListBox control)
                {
                    foreach (object xItem in control.Items)
                    {
                        ListBoxItem? xItemAlt = control.ItemContainerGenerator.ContainerFromItem (xItem) as ListBoxItem;

                        if (xItemAlt != null && xItemAlt.IsFocused)
                            return (control.IsFocused, control.SelectedIndex, true);
                    }

                    return (control.IsFocused, control.SelectedIndex, false);
                }

                var xSoon_Info = iGetいろいろ (mSoon);
                var xNow_Info = iGetいろいろ (mNow);
                var xHandled_Info = iGetいろいろ (mHandled);

                mTasksToBeHandledSoon.Clear ();
                mTasksToBeHandledNow.Clear ();
                mHandledTasks.Clear ();

                long xOneWeekAgoUtc = DateTime.UtcNow.AddDays (-7).Ticks;

                foreach (TaskInfo xTask in xAllTasks)
                {
                    if (xTask.HandlingUtc != null)
                    {
                        if (xTask.HandlingUtc.Value > xOneWeekAgoUtc)
                            mHandledTasks.Add (xTask);
                    }

                    else
                    {
                        if (xTask.State == TaskState.Soon)
                            mTasksToBeHandledSoon.Add (xTask);

                        else if (xTask.State == TaskState.Now)
                            mTasksToBeHandledNow.Add (xTask);
                    }
                }

                void iRestoreいろいろ (ListBox control, (bool IsControlFocused, int SelectedIndex, bool IsItemFocused) info)
                {
                    if (info.IsControlFocused)
                        control.Focus ();

                    iShared.UpdateListBoxItemSelection (control, info.SelectedIndex, info.IsItemFocused);
                }

                iRestoreいろいろ (mSoon, xSoon_Info);
                iRestoreいろいろ (mNow, xNow_Info);
                iRestoreいろいろ (mHandled, xHandled_Info);

                mLastReloadingLocalTimeString.Text = "最終: " + DateTime.Now.ToString ("H':'mm':'ss", CultureInfo.InvariantCulture);

                if (iShared.IsLogging)
                {
                    StringBuilder xBuilder = new StringBuilder ();

                    if (File.Exists (mLogFilePath))
                        xBuilder.AppendLine ();

                    xBuilder.AppendLine ($"[{DateTime.UtcNow.ToString ("R")}]");
                    xBuilder.Append ($"Reloaded {xAllTasks.Count.ToString (CultureInfo.InvariantCulture)} tasks ");
                    xBuilder.Append ($"from {taskKillerDirectoryPaths.Length.ToString (CultureInfo.InvariantCulture)} lists ");
                    xBuilder.AppendLine ($"in {xStopwatch.ElapsedMilliseconds.ToString (CultureInfo.InvariantCulture)}ms.");

                    File.AppendAllText (mLogFilePath, xBuilder.ToString (), Encoding.UTF8);
                }
            }

            finally
            {
                if (xLockTaken)
                    Monitor.Exit (mReloadingLocker);
            }
        }

        private Task? mReloadingTask = null;

        private bool mContinuesReloading = false;

        private DateTime? mPreviousAutoReloadingUtc = null;

        private int mAutoReloadingIntervalInMilliseconds = 30_000;

        private void mWindow_Initialized (object sender, EventArgs e)
        {
            try
            {
                if (int.TryParse (ConfigurationManager.AppSettings ["InitialWidth"], out int xWidth))
                    Width = xWidth;

                if (int.TryParse (ConfigurationManager.AppSettings ["InitialHeight"], out int xHeight))
                    Height = xHeight;

                if ("True".Equals (ConfigurationManager.AppSettings ["IsMaximized"], StringComparison.OrdinalIgnoreCase))
                    WindowState = WindowState.Maximized;

                TextOptions.SetTextFormattingMode (this, iShared.TextFormattingMode);
                TextOptions.SetTextHintingMode (this, iShared.TextHintingMode);
                TextOptions.SetTextRenderingMode (this, iShared.TextRenderingMode);

                string? xFontFamily = ConfigurationManager.AppSettings ["FontFamily"];

                if (string.IsNullOrEmpty (xFontFamily) == false)
                    mWindow.FontFamily = new FontFamily (xFontFamily);

                if (double.TryParse (ConfigurationManager.AppSettings ["ListBoxFontSize"], out double xResult))
                {
                    mSoon.FontSize = xResult;
                    mNow.FontSize = xResult;
                    mHandled.FontSize = xResult;
                }

                if (int.TryParse (ConfigurationManager.AppSettings ["AutoReloadingIntervalInMilliseconds"], out int xResultAlt))
                    mAutoReloadingIntervalInMilliseconds = xResultAlt;

                // ソートでちょっと詰まった
                // ソートされず、TaskInfo.ToString で OrderingUtc などを日時にして表示してみると同じ値になった
                // 原因は二つで、1) ここではプロパティーしか指定できないのにフィールドを使っていたため、
                //     2) 同じ値になったのは、Ticks に連番を足し引きする実装により、「秒」には差が現れないため

                // ここで追加したソート関連の情報は、Clear でもなくならない
                // コレクションとコントロールの間に暗黙的かつ自動的に作られるビューへの追加だからか

                // Applying a sort description doesn't appear to actually sort the underlying data source
                // https://social.msdn.microsoft.com/Forums/vstudio/en-US/81d65830-031f-492a-8bc8-1ff102435437/applying-a-sort-description-doesnt-appear-to-actually-sort-the-underlying-data-source

                CollectionViewSource.GetDefaultView (mTasksToBeHandledSoon).SortDescriptions.Add (
                    new SortDescription ("OrderingUtcProperty", ListSortDirection.Ascending));

                CollectionViewSource.GetDefaultView (mTasksToBeHandledNow).SortDescriptions.Add (
                    new SortDescription ("OrderingUtcProperty", ListSortDirection.Ascending));

                CollectionViewSource.GetDefaultView (mHandledTasks).SortDescriptions.Add (
                    new SortDescription ("HandlingUtcProperty", ListSortDirection.Descending));

                mSoon.ItemsSource = mTasksToBeHandledSoon;
                mNow.ItemsSource = mTasksToBeHandledNow;
                mHandled.ItemsSource = mHandledTasks;
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void mWindow_Loaded (object sender, RoutedEventArgs e)
        {
            try
            {
                iShared.IsMainWindowClosed = false;

                mContinuesReloading = true;

                mReloadingTask = Task.Run (() =>
                {
                    while (mContinuesReloading)
                    {
                        DateTime xUtcNow = DateTime.UtcNow;

                        if (mPreviousAutoReloadingUtc == null ||
                            (xUtcNow - mPreviousAutoReloadingUtc.Value).TotalMilliseconds >= mAutoReloadingIntervalInMilliseconds)
                        {
                            if (iShared.IsMainWindowClosed == false)
                            {
                                // iReload 側でコリジョンのチェックが防止されるため、呼びっぱなしでいい
                                // 既にリロード中で、このスレッドによるリロードが却下されるなら、
                                //     結果は同じということで mPreviousAutoReloadingUtc を更新

                                mPreviousAutoReloadingUtc = xUtcNow;

                                mWindow.Dispatcher.Invoke (() =>
                                {
                                    try
                                    {
                                        // 全てのタスクのリロードに比べると軽い処理なので、タスクリストのパスも毎回再取得されるように変更
                                        // また、ほかのところでは iReload が catch されているのにここではそうでないことが自動リロードの失敗につながっていたので対処

                                        // iReload には、タスクリストっぽいディレクトリーに Tasks ディレクトリーが含まれないときに落ちる問題がある
                                        // ほかのケースも含めてチェックし、問題があるときのみタスクリストのパスを再取得するのも選択肢だが、めんどくさい

                                        iReload (reloadsParametersToo: true);
                                    }

                                    catch
                                    {
                                    }
                                });
                            }
                        }

                        Thread.Sleep (100);
                    }
                });

                mReload.Focus ();
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void mWindow_PreviewKeyDown (object sender, KeyEventArgs e)
        {
            try
            {
                // Ctrl キーなどとセットのものも含めて全ての Space キー押下を取るが、ほかで困ることはなさそう → Space をほかで使うため F5 に変更
                // リロード中のため新たなリロードが行われなくても、キーの処理は終わったものと見なされる

                if (e.Key == Key.F5)
                {
                    iReload (reloadsParametersToo: true);
                    e.Handled = true;
                }
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void iChangeSelectedTasksState (ListBox control)
        {
            if (control.SelectedItem != null)
            {
                bool xLockTaken = false;

                try
                {
                    // リロードの方法は、現時点では、別スレッド、ボタン、F5 キーの三つ
                    // いずれも iReload を呼び、そちらで「既にリロード中でない場合のみリロード」なので、ここでのコリジョンはない

                    // 状態の変更は、ファイルの更新と ListBox の更新を、lock に相当するコードブロック内で一度に
                    // リロード中なら終わるのを待つ
                    // 状態の変更中にリロードが試みられるとコリジョン扱いされてリロードの方がキャンセルされるのは、発生確率の低さおよび実害の小ささから気にしない

                    // フラグを立てて最後にこちらでリロードも考えた
                    // 状態の変更はユーザー操作によるため、これと当たるリロードの方がボタンや F5 キーによることはない
                    // それはつまり、「押したのにリロードしない」はありえないということ
                    // 状態を変更したときにたまたま別スレッドによる自動リロードとぶつかりそちらが動かなくても、
                    //     「そろそろ自動リロードのタイミングだ」という認知がユーザー側にないため、コリジョンに気づくことすらない
                    // その後も30秒ごとに自動リロードが試みられるため、多少ぶつかろうと、そのうちリロードされる

                    // 以下、lock で書いてもいいが、別のところで TryEnter を呼ぶため、サンプルコードを残しておく

                    // Note If no exception occurs, the output of this method is always true とのことだが、作法として lockTaken の値を拾う
                    // 「lock はこういうコードに展開される」というページで必ず拾われているため

                    // Monitor.Enter Method (System.Threading) | Microsoft Learn
                    // https://learn.microsoft.com/en-us/dotnet/api/system.threading.monitor.enter

                    Monitor.Enter (mReloadingLocker, ref xLockTaken);

                    TaskInfo xTask = (TaskInfo) control.SelectedItem;
                    int xSelectedIndex = control.SelectedIndex;

                    StateManager xStateManager = new StateManager (xTask.TaskListDirectoryPath);

                    if (control == mSoon)
                    {
                        xStateManager.SafeSetValue (xTask.Guid.ToString ("D"), TaskState.Now);

                        mTasksToBeHandledSoon.Remove (xTask);
                        mTasksToBeHandledNow.Add (xTask);

                        iShared.UpdateListBoxItemSelection (mSoon, xSelectedIndex, true);
                    }

                    else
                    {
                        xStateManager.SafeSetValue (xTask.Guid.ToString ("D"), TaskState.Soon);

                        mTasksToBeHandledNow.Remove (xTask);
                        mTasksToBeHandledSoon.Add (xTask);

                        iShared.UpdateListBoxItemSelection (mNow, xSelectedIndex, true);
                    }
                }

                finally
                {
                    if (xLockTaken)
                        Monitor.Exit (mReloadingLocker);
                }
            }
        }

        private static void iOpenSelectedTasksList (ListBox control)
        {
            if (control.SelectedItem != null)
            {
                TaskInfo xTask = (TaskInfo) control.SelectedItem;

                if (File.Exists (Path.Join (xTask.TaskListDirectoryPath, "Running.txt")) == false)
                {
                    // すぐ Dispose してよいのか分からないが、作法として
                    // 今のところ問題なさそう

                    using (Process.Start (Path.Join (xTask.TaskListDirectoryPath, "taskKiller.exe"), new string [] { "-SelectTask", xTask.Guid.ToString ("D") }))
                    {
                    }
                }
            }
        }

        private static void iFocusOnSelectedItemOrList (ListBox control)
        {
            if (control.SelectedIndex >= 0)
                iShared.UpdateListBoxItemSelection (control, control.SelectedIndex, true);

            else control.Focus ();
        }

        private static void iCopySelectedItemsTextToClipboard (ListBox control)
        {
            if (control.SelectedItem != null)
            {
                TaskInfo xTask = (TaskInfo) control.SelectedItem;
                Clipboard.SetText (xTask.Contents);
            }
        }

        private void mSoon_PreviewKeyDown (object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Space)
                {
                    iChangeSelectedTasksState (mSoon);
                    e.Handled = true;
                }

                else if (e.Key == Key.Enter)
                {
                    iOpenSelectedTasksList (mSoon);
                    e.Handled = true;
                }

                else if (e.Key == Key.Right)
                {
                    iFocusOnSelectedItemOrList (mNow);
                    e.Handled = true;
                }

                else if (e.Key == Key.C && Keyboard.Modifiers.HasFlag (ModifierKeys.Control))
                {
                    iCopySelectedItemsTextToClipboard (mSoon);
                    e.Handled = true;
                }
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void mNow_PreviewKeyDown (object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Space)
                {
                    iChangeSelectedTasksState (mNow);
                    e.Handled = true;
                }

                else if (e.Key == Key.Enter)
                {
                    iOpenSelectedTasksList (mNow);
                    e.Handled = true;
                }

                else if (e.Key == Key.Left)
                {
                    iFocusOnSelectedItemOrList (mSoon);
                    e.Handled = true;
                }

                else if (e.Key == Key.Right)
                {
                    iFocusOnSelectedItemOrList (mHandled);
                    e.Handled = true;
                }

                else if (e.Key == Key.C && Keyboard.Modifiers.HasFlag (ModifierKeys.Control))
                {
                    iCopySelectedItemsTextToClipboard (mNow);
                    e.Handled = true;
                }
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void mHandled_PreviewKeyDown (object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Enter)
                {
                    iOpenSelectedTasksList (mHandled);
                    e.Handled = true;
                }

                else if (e.Key == Key.Left)
                {
                    iFocusOnSelectedItemOrList (mNow);
                    e.Handled = true;
                }

                else if (e.Key == Key.C && Keyboard.Modifiers.HasFlag (ModifierKeys.Control))
                {
                    iCopySelectedItemsTextToClipboard (mHandled);
                    e.Handled = true;
                }
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void mReload_Click (object sender, RoutedEventArgs e)
        {
            try
            {
                // iReload 側でコリジョンのチェックが防止されるため、呼びっぱなしでいい
                iReload (reloadsParametersToo: true);
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void mClose_Click (object sender, RoutedEventArgs e)
        {
            try
            {
                Close ();
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void mWindow_Closing (object sender, CancelEventArgs e)
        {
            try
            {
                if (MessageBox.Show (this, "プログラムを終了しますか？", string.Empty, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.No)
                    e.Cancel = true;
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }

        private void mWindow_Closed (object sender, EventArgs e)
        {
            try
            {
                iShared.IsMainWindowClosed = true;

                // ウィンドウが破棄されたあとで、ほかのスレッドが Invoke してくるのを防ぐ
                // 100ミリ秒ごとに mContinuesReloading を見るループだが、
                //     タスクのファイルが多ければキャッシュありでも1秒くらいかかる可能性があるので3秒待つ

                mContinuesReloading = false;
                mReloadingTask?.Wait (3000);
            }

            catch (Exception xException)
            {
                iShared.HandleException (this, xException);
            }
        }
    }
}
