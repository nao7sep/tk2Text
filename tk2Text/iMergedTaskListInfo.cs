using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nekote;

namespace tk2Text
{
    internal class iMergedTaskListInfo
    {
        public readonly List <iTaskListDirectoryInfo> Directories = new List <iTaskListDirectoryInfo> ();

        public readonly iAttributesInfo Attributes;

        public iMergedTaskListInfo (iAttributesInfo attributes)
        {
            Attributes = attributes;
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

        private IEnumerable <iAttachedFileInfo>? mAttachedFiles;

        public IEnumerable <iAttachedFileInfo> AttachedFiles
        {
            get
            {
                if (mAttachedFiles == null)
                    mAttachedFiles = Directories.SelectMany (x => x.AttachedFiles);

                return mAttachedFiles;
            }
        }
    }
}
