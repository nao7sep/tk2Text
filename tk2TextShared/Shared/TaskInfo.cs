using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tk2Text
{
    public class TaskInfo
    {
        public Guid Guid;

        public long CreationUtc;

        public string? Contents;

        public TaskState State;

        public bool IsSpecial = false;

        public long? HandlingUtc;

        public Guid? RepeatedTaskGuid;

        public List <NoteInfo> Notes = new List <NoteInfo> ();

        public long OrderingUtc;

        #region tkView 用
        // TaskListTitle と同じ理由で null チェックを不要に
        public string TaskListDirectoryPath = string.Empty;

        // Settings.txt の内容またはディレクトリー名
        // 設定される実装なら必ず値が入るため、null チェックを不要に
        public string TaskListTitle = string.Empty;

        // ListBox の内容のソートは、プロパティーでないと動かない

        public long OrderingUtcProperty
        {
            get
            {
                return OrderingUtc;
            }
        }

        // HandlingUtc が null でない場合のみタスクが入るリスト内でのソートのためのものなので Nullable にしない

        public long HandlingUtcProperty
        {
            get
            {
                return HandlingUtc!.Value;
            }
        }

        public override string ToString ()
        {
            if (HandlingUtc == null)
                return $"{Contents} ({TaskListTitle})";

            // [*] ... も考えたが、A → B というシンプルな関係性を表現するだけに最初の [ は無駄な文字
            else return $"{(State == TaskState.Done ? "完了" : "却下")}: {Contents} ({TaskListTitle})";
        }
        #endregion
    }
}
