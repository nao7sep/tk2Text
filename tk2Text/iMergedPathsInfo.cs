using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tk2Text
{
    internal struct iMergedPathsInfo
    {
        public string LeftPath;

        public string RightPath;

        public iMergedPathsInfo (string leftPath, string rightPath)
        {
            LeftPath = leftPath;
            RightPath = rightPath;
        }
    }
}
