namespace Soddi.Tasks;

public interface ITask
{
    public Task GoAsync(IProgress<(string taskId, string message, double weight, double maxValue)> progress, CancellationToken cancellationToken);
    public double GetTaskWeight();
}
