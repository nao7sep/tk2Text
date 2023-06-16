using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nekote;

namespace tk2Text
{
    public class StateManager
    {
        public readonly string DirectoryPath;

        public StateManager (string appDirectoryPath)
        {
            DirectoryPath = Path.Join (appDirectoryPath, "States");
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

        public TaskState SafeGetValue (string guidString)
        {
            try
            {
                return nFile.ReadAllText (GetFilePath (guidString)).nNormalizeLine ().nToEnum <TaskState> ();
            }

            catch
            {
                return default;
            }
        }

        public void SafeSetValue (string guidString, TaskState value)
        {
            try
            {
                // nDirectory.Create (StatesDirectoryPath);
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
