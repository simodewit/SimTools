using System;
using System.Windows.Threading;

namespace SimTools.Services
{
    public sealed class DebouncedSaver : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private readonly Action _save;

        public DebouncedSaver(Action save, TimeSpan? delay = null)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _timer = new DispatcherTimer { Interval = delay ?? TimeSpan.FromMilliseconds(700) };
            _timer.Tick += (s, e) => { _timer.Stop(); _save(); };
        }

        public void Queue()
        {
            _timer.Stop();
            _timer.Start();
        }

        public void Dispose() => _timer.Stop();
    }
}
