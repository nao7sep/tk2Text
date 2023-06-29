using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nekote;

namespace tk2Text
{
    internal class iTaskListDirectoryInfo
    {
        public readonly string Path;

        public iTaskListDirectoryInfo (string path)
        {
            Path = path;
        }

        private IEnumerable <TaskInfo>? mAllTasks;

        public IEnumerable <TaskInfo> AllTasks
        {
            get
            {
                if (mAllTasks == null)
                {
                    string xTasksDirectoryPath = nPath.Combine (Path, "Tasks");

                    // iParametersValidator で見るため不要だが、作法として

                    if (nDirectory.Exists (xTasksDirectoryPath) == false)
                        mAllTasks = Enumerable.Empty <TaskInfo> ();

                    else
                    {
                        List <TaskInfo> xTasks = new List <TaskInfo> ();

                        StateManager xStateManager = new StateManager (Path);
                        OrderManager xOrderManager = new OrderManager (Path);

                        foreach (string xFilePath in Directory.GetFiles (xTasksDirectoryPath, "*.*", SearchOption.TopDirectoryOnly))
                        {
                            try
                            {
                                TaskInfo xTask = Shared.LoadTask (xFilePath, xStateManager, xOrderManager);
                                xTasks.Add (xTask);
                            }

                            catch
                            {
                                // おかしいファイルは、このアプリでは無視される
                                // ほかのアプリでチェックされる
                            }
                        }

                        mAllTasks = xTasks;
                    }
                }

                return mAllTasks;
            }
        }
    }
}
