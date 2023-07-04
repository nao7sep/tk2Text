using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Nekote;

namespace tk2Text
{
    internal static class iShared
    {
        public static readonly string AppDirectoryPath = nPath.GetDirectoryPath (Assembly.GetExecutingAssembly ().Location);
    }
}
