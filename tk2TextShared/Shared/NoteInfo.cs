using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tk2Text
{
    public class NoteInfo
    {
        public Guid Guid;

        public long CreationUtc;

        public string? Contents;

        public TaskInfo? ParentTask;
    }
}
