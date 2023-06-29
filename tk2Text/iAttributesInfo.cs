using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nekote;

namespace tk2Text
{
    internal struct iAttributesInfo
    {
        public string SourceDirectoryPath;

        public string DestDirectoryPath;

        public string DestFileName;

        public string Title;

        public iAttributesInfo (string sourceDirectoryPath, string destDirectoryPath, string destFileName, string title)
        {
            SourceDirectoryPath = sourceDirectoryPath;
            DestDirectoryPath = destDirectoryPath;
            DestFileName = destFileName;
            Title = title;
        }

        public static readonly iAttributesInfo Empty = new iAttributesInfo
        {
            SourceDirectoryPath = string.Empty,
            DestDirectoryPath = string.Empty,
            DestFileName = string.Empty,
            Title = string.Empty
        };
    }
}
