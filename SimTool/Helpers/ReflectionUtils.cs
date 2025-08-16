using System;
using System.Collections;
using System.Reflection;
using System.Windows.Input;

namespace SimTools.Helpers
{
    public static class ReflectionUtils
    {
        public static bool TryExecuteCommand(object target, string propertyName)
        {
            var p = target?.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            var cmd = p?.GetValue(target) as ICommand;
            if (cmd != null && cmd.CanExecute(null)) { cmd.Execute(null); return true; }
            return false;
        }

        public static bool TryInvoke(object target, string methodName)
        {
            var m = target?.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (m != null) { try { m.Invoke(target, null); return true; } catch { } }
            return false;
        }

        public static bool TrySetStringProperty(object target, string[] propNames, string value)
        {
            if (target == null) return false;
            foreach (var name in propNames)
            {
                var p = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (p != null && p.CanWrite && p.PropertyType == typeof(string))
                {
                    try { p.SetValue(target, value, null); return true; } catch { }
                }
            }
            return false;
        }

        public static string GetStringProperty(object target, string name)
        {
            var p = target?.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            return p?.GetValue(target) as string;
        }

        public static object GetProperty(object target, string name)
            => target?.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(target);

        public static IList GetListProperty(object target, string name)
            => GetProperty(target, name) as IList;

        public static object GetCurrentProfile(object vm)
        {
            if (vm == null) return null;
            foreach (var n in new[] { "SelectedProfile", "CurrentProfile" })
            {
                var p = vm.GetType().GetProperty(n, BindingFlags.Instance | BindingFlags.Public);
                if (p != null) return p.GetValue(vm);
            }
            return null;
        }
    }
}
