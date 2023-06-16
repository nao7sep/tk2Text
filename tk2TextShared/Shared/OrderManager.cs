using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nekote;

namespace tk2Text
{
    public class OrderManager
    {
        public readonly string DirectoryPath;

        public OrderManager (string appDirectoryPath)
        {
            DirectoryPath = Path.Join (appDirectoryPath, "Ordering");
        }

        public string GetFilePath (string guidString)
        {
            return Path.Join (DirectoryPath, guidString + ".txt");
        }

        public bool ContainsKey (string guidString)
        {
            try
            {
                return nFile.Exists (GetFilePath (guidString));
            }

            catch
            {
                return false;
            }
        }

        public long SafeGetUtc (string guidString)
        {
            try
            {
                return nFile.ReadAllText (GetFilePath (guidString)).nNormalizeLine ().nToLong ();
            }

            catch
            {
                return -1;
            }
        }

        public void SafeSetUtc (string guidString, long value)
        {
            try
            {
                // nDirectory.Create (OrderingDirectoryPath);
                nFile.WriteAllText (GetFilePath (guidString), value.nToString ());
            }

            catch
            {
            }
        }

        public void SafeDeleteFile (string guidString)
        {
            try
            {
                string xFilePath = GetFilePath (guidString);

                if (nFile.Exists (xFilePath))
                    nFile.Delete (xFilePath);

                if (nDirectory.IsEmpty (DirectoryPath))
                    nDirectory.Delete (DirectoryPath);
            }

            catch
            {
            }
        }
    }
}
