namespace Soddi.Tasks;

public interface ITask
{
    public void Go(IProgress<(string taskId, string message, double weight, double maxValue)> progress);
    public double GetTaskWeight();
}