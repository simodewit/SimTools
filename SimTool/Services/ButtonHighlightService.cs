using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using SimTools.Helpers;

namespace SimTools.Services
{
    public sealed class ButtonHighlightService
    {
        private readonly Dictionary<Button, Brush> _lit = new Dictionary<Button, Brush>();
        private readonly Brush _accent;

        public ButtonHighlightService()
        {
            _accent = KeybindUiHelpers.TryFindBrush("Brush.Accent", new SolidColorBrush(Color.FromRgb(80, 200, 160)));
        }

        public void Highlight(Button btn)
        {
            if (btn == null) return;
            if (_lit.ContainsKey(btn)) return;
            _lit[btn] = btn.Background;
            btn.Background = _accent;
        }

        public void Unhighlight(Button btn)
        {
            if (btn == null) return;
            if (_lit.TryGetValue(btn, out var original))
            {
                btn.Background = original;
                _lit.Remove(btn);
            }
        }

        public void ClearAll()
        {
            foreach (var kv in _lit.ToList())
            {
                kv.Key.Background = kv.Value;
                _lit.Remove(kv.Key);
            }
        }

        public void ClearRowsExcept(params Button[] keep)
        {
            var keepSet = new HashSet<Button>(keep ?? new Button[0]);
            foreach (var kv in _lit.ToList())
            {
                if (keepSet.Contains(kv.Key)) continue;
                kv.Key.Background = kv.Value;
                _lit.Remove(kv.Key);
            }
        }
    }
}
