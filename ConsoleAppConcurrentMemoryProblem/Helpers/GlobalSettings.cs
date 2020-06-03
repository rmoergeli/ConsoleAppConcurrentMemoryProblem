using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ConsoleAppConcurrentMemoryProblem.Helpers
{
    class GlobalSettings
    {
        public static DriveInfo systemdrive;

        public GlobalSettings()
        {
            systemdrive = new DriveInfo(System.IO.Path.GetPathRoot(Environment.SystemDirectory));
        }
    }
}
