using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SimTools.Helpers;
using SimTools.Models;

namespace SimTools.Services
{
    public sealed class MapHotkeyController
    {
        private DateTime _lastNextUtc = DateTime.MinValue;
        private DateTime _lastPrevUtc = DateTime.MinValue;
        private readonly TimeSpan _debounce;

        private readonly Dispatcher _dispatcher;
        private readonly ButtonHighlightService _lights;

        public MapHotkeyController(Dispatcher dispatcher, ButtonHighlightService lights, TimeSpan? debounce = null)
        {
            _dispatcher = dispatcher ?? Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            _lights = lights ?? new ButtonHighlightService();
            _debounce = debounce ?? TimeSpan.FromMilliseconds(250);
        }

        private bool ShouldFire(ref DateTime last)
        {
            var now = DateTime.UtcNow;
            if ((now - last) < _debounce) return false;
            last = now;
            return true;
        }

        public void OnInput(InputBindingResult fired, DeviceBindingDescriptor next, DeviceBindingDescriptor prev,
                            Action nextCmd, Action prevCmd, Button nextBtn, Button prevBtn)
        {
            if (fired == null) return;

            if (DeviceBindingDescriptor.IsMatch(next, fired) && ShouldFire(ref _lastNextUtc))
            {
                if (!_dispatcher.CheckAccess())
                {
                    _dispatcher.BeginInvoke(new Action(() => Execute(nextCmd, nextBtn)));
                }
                else Execute(nextCmd, nextBtn);
            }

            if (DeviceBindingDescriptor.IsMatch(prev, fired) && ShouldFire(ref _lastPrevUtc))
            {
                if (!_dispatcher.CheckAccess())
                {
                    _dispatcher.BeginInvoke(new Action(() => Execute(prevCmd, prevBtn)));
                }
                else Execute(prevCmd, prevBtn);
            }
        }

        private void Execute(Action command, Button btn)
        {
            if (btn != null) _lights.Highlight(btn);
            command?.Invoke();
        }
    }
}
