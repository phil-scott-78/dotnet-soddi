using System;
using ShellProgressBar;

namespace Soddi
{
    /// <summary>
    /// Wrapper around <see cref="ProgressBar"/> that either catches up or throttles
    /// down progress depending on what the value should be at that point in time
    /// </summary>
    internal class FudgedProgressBar : IDisposable
    {
        private readonly ProgressBar _progressBar;
        private int _tickCountShouldBe;

        public FudgedProgressBar(int maxTicks, string message, ConsoleColor color)
        {
            _progressBar = new ProgressBar(maxTicks, message, color);
        }

        public void Tick(int incrementAmount, string message)
        {
            var nextTick = _progressBar.CurrentTick + incrementAmount;

            // if we get ahead then let's keep displaying value that's high
            // and let the current value catch up. we shouldn't be too off.
            _progressBar.Tick(nextTick > _tickCountShouldBe ? nextTick : _tickCountShouldBe, message);
        }

        public void AddTaskWeight(int weight)
        {
            _tickCountShouldBe += weight;
        }

        public void WrapUp(string message)
        {
            _progressBar.Tick(_progressBar.MaxTicks, message);
        }

        public void Dispose()
        {
            _progressBar.Dispose();
        }
    }
}
