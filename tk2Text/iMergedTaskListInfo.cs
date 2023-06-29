using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tk2Text
{
    internal class iMergedTaskListInfo
    {
        public readonly List <iTaskListDirectoryInfo> Directories = new List <iTaskListDirectoryInfo> ();

        public readonly string DestPath;

        public readonly string Title;

        public iMergedTaskListInfo (string destPath, string title)
        {
            DestPath = destPath;
            Title = title;
        }

        private IEnumerable <TaskInfo>? mAllTasks;

        public IEnumerable <TaskInfo> AllTasks
        {
            get
            {
                if (mAllTasks == null)
                    mAllTasks = Directories.SelectMany (x => x.AllTasks);

                return mAllTasks;
            }
        }

        private IEnumerable <TaskInfo>? mAllButMemoTasks;

        public IEnumerable <TaskInfo> AllButMemoTasks
        {
            get
            {
                if (mAllButMemoTasks == null)
                    mAllButMemoTasks = AllTasks.Where (x => string.Equals (x.Contents, "メモ") == false);

                return mAllButMemoTasks;
            }
        }

        private IEnumerable <NoteInfo>? mAllMemoNotes;

        public IEnumerable <NoteInfo> AllMemoNotes
        {
            get
            {
                if (mAllMemoNotes == null)
                    mAllMemoNotes = AllTasks.Where (x => string.Equals (x.Contents, "メモ")).SelectMany (y => y.Notes);

                return mAllMemoNotes;
            }
        }
    }
}
