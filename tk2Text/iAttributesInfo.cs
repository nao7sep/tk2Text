using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tk2Text
{
    internal struct iAttributesInfo
    {
        public string SourcePath;

        public string DestPath;

        public string Title;

        public iAttributesInfo (string sourcePath, string destPath, string title)
        {
            SourcePath = sourcePath;
            DestPath = destPath;
            Title = title;
        }

        public static readonly iAttributesInfo Empty = new iAttributesInfo
        {
            SourcePath = string.Empty,
            DestPath = string.Empty,
            Title = string.Empty
        };
    }
}
