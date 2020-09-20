using System;

namespace Soddi.Tasks
{
    public interface ITask
    {
        public void Go(IProgress<(string message, int weight)> progress);
        public int GetTaskWeight();
    }
}
