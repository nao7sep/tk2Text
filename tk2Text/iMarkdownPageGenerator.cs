using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tk2Text
{
    internal class iMarkdownPageGenerator
    {
        public readonly iParametersStringParser Parser;

        public readonly iMergedTaskListInfo MergedTaskList;

        public iMarkdownPageGenerator (iParametersStringParser parser, iMergedTaskListInfo mergedTaskList)
        {
            Parser = parser;
            MergedTaskList = mergedTaskList;
        }
    }
}
