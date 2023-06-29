using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tk2Text
{
    internal class iAttachedFileInfo
    {
        public readonly FileInfo File;

        public readonly Guid Guid;

        public readonly DateTime AttachedAtUtc;

        public readonly DateTime ModifiedAtUtc;

        public iAttachedFileInfo (string path, Guid guid, DateTime attachedAtUtc, DateTime modifiedAtUtc)
        {
            File = new FileInfo (path);
            Guid = guid;
            AttachedAtUtc = attachedAtUtc;
            ModifiedAtUtc = modifiedAtUtc;
        }
    }
}
