using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;

namespace SimTools.Services
{
    /// <summary>
    /// Wires to VM collections (Profiles/Maps/Keybinds) and tracks their items' INotifyPropertyChanged.
    /// Emits a single Changed event whenever anything changes.
    /// </summary>
    public sealed class VmCollectionBinder : IDisposable
    {
        private readonly string[] _collectionPropertyNames;
        private readonly List<INotifyPropertyChanged> _trackedItems = new List<INotifyPropertyChanged>();
        private readonly Dictionary<string, INotifyCollectionChanged> _wiredCollections = new Dictionary<string, INotifyCollectionChanged>();
        private INotifyPropertyChanged _vmInpc;

        public event EventHandler Changed;

        public VmCollectionBinder(params string[] collectionPropertyNames)
        {
            _collectionPropertyNames = collectionPropertyNames ?? Array.Empty<string>();
        }

        public void Wire(object vm)
        {
            Unwire();

            if (vm == null) return;

            _vmInpc = vm as INotifyPropertyChanged;
            if (_vmInpc != null) _vmInpc.PropertyChanged += VmOnPropertyChanged;

            foreach (var name in _collectionPropertyNames)
            {
                var col = GetCollection(vm, name);
                if (col != null)
                {
                    _wiredCollections[name] = col;
                    WireCollection(col);
                }
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Unwire()
        {
            if (_vmInpc != null) _vmInpc.PropertyChanged -= VmOnPropertyChanged;
            _vmInpc = null;

            foreach (var kv in _wiredCollections)
                UnwireCollection(kv.Value);
            _wiredCollections.Clear();

            foreach (var it in _trackedItems)
                it.PropertyChanged -= ItemOnPropertyChanged;
            _trackedItems.Clear();
        }

        private void VmOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!_wiredCollections.ContainsKey(e.PropertyName)) return;

            // swap collection instance
            var vm = sender;
            UnwireCollection(_wiredCollections[e.PropertyName]);
            var newCol = GetCollection(vm, e.PropertyName);
            _wiredCollections[e.PropertyName] = newCol;
            WireCollection(newCol);

            Changed?.Invoke(this, EventArgs.Empty);
        }

        private static INotifyCollectionChanged GetCollection(object vm, string propName)
        {
            if (vm == null) return null;
            var p = vm.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public);
            return p?.GetValue(vm) as INotifyCollectionChanged;
        }

        private void WireCollection(INotifyCollectionChanged col)
        {
            if (col == null) return;
            col.CollectionChanged += OnCollectionChanged;

            if (col is IEnumerable enumerable)
            {
                foreach (var obj in enumerable)
                {
                    if (obj is INotifyPropertyChanged inpc && !_trackedItems.Contains(inpc))
                    {
                        inpc.PropertyChanged += ItemOnPropertyChanged;
                        _trackedItems.Add(inpc);
                    }
                }
            }
        }

        private void UnwireCollection(INotifyCollectionChanged col)
        {
            if (col == null) return;
            col.CollectionChanged -= OnCollectionChanged;

            if (col is IEnumerable enumerable)
            {
                foreach (var obj in enumerable)
                {
                    if (obj is INotifyPropertyChanged inpc && _trackedItems.Contains(inpc))
                    {
                        inpc.PropertyChanged -= ItemOnPropertyChanged;
                        _trackedItems.Remove(inpc);
                    }
                }
            }
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (var obj in e.NewItems)
                    if (obj is INotifyPropertyChanged inpc && !_trackedItems.Contains(inpc))
                    {
                        inpc.PropertyChanged += ItemOnPropertyChanged;
                        _trackedItems.Add(inpc);
                    }

            if (e.OldItems != null)
                foreach (var obj in e.OldItems)
                    if (obj is INotifyPropertyChanged inpc && _trackedItems.Contains(inpc))
                    {
                        inpc.PropertyChanged -= ItemOnPropertyChanged;
                        _trackedItems.Remove(inpc);
                    }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void ItemOnPropertyChanged(object sender, PropertyChangedEventArgs e)
            => Changed?.Invoke(this, EventArgs.Empty);

        public void Dispose() => Unwire();
    }
}
