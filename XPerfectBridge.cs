using System;
using System.Reflection;

namespace CheryTools
{
    internal static class XPerfectBridge
    {
        public enum Judge
        {
            None = 0,
            X = 1,
            Plus = 2,
            Minus = 3
        }

        private static bool _resolved;
        private static bool _installed;
        private static Type _accuracyStateType;
        private static MemberInfo _lastJudgeMember;
        private static MemberInfo _lastJudgeForTextMember;
        private static MemberInfo _xCountMember;
        private static MemberInfo _plusCountMember;
        private static MemberInfo _minusCountMember;
        private static PropertyInfo _enabledProperty;

        public static bool Installed
        {
            get
            {
                EnsureResolved();
                return _installed;
            }
        }

        public static bool Active
        {
            get
            {
                if (!Installed) return false;
                try
                {
                    if (_enabledProperty == null) return true;
                    object value = _enabledProperty.GetValue(null, null);
                    return value is bool enabled && enabled;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static void RefreshDetection()
        {
            _resolved = false;
            _installed = false;
            _accuracyStateType = null;
            _lastJudgeMember = null;
            _lastJudgeForTextMember = null;
            _xCountMember = null;
            _plusCountMember = null;
            _minusCountMember = null;
            _enabledProperty = null;
            EnsureResolved();
        }

        public static Judge LastJudge()
        {
            return ReadJudgeMember(_lastJudgeMember);
        }

        public static Judge LastJudgeForText()
        {
            Judge judge = ReadJudgeMember(_lastJudgeForTextMember);
            return judge != Judge.None ? judge : LastJudge();
        }

        public static int XPerfectCount()
        {
            return ReadIntMember(_xCountMember);
        }

        public static int PlusPerfectCount()
        {
            return ReadIntMember(_plusCountMember);
        }

        public static int MinusPerfectCount()
        {
            return ReadIntMember(_minusCountMember);
        }

        private static Judge ReadJudgeMember(MemberInfo member)
        {
            if (!Active || member == null) return Judge.None;
            try
            {
                object value = ReadStaticMember(member);
                if (value == null) return Judge.None;
                int raw = Convert.ToInt32(value);
                if (raw < 0 || raw > 3) return Judge.None;
                return (Judge)raw;
            }
            catch
            {
                return Judge.None;
            }
        }

        private static int ReadIntMember(MemberInfo member)
        {
            if (!Active || member == null) return 0;
            try
            {
                object value = ReadStaticMember(member);
                return value == null ? 0 : Convert.ToInt32(value);
            }
            catch
            {
                return 0;
            }
        }

        private static object ReadStaticMember(MemberInfo member)
        {
            PropertyInfo property = member as PropertyInfo;
            if (property != null) return property.GetValue(null, null);

            FieldInfo field = member as FieldInfo;
            return field != null ? field.GetValue(null) : null;
        }

        private static MemberInfo GetStaticReadable(Type type, string name)
        {
            if (type == null || string.IsNullOrEmpty(name)) return null;
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

            PropertyInfo property = type.GetProperty(name, Flags);
            if (property != null && property.GetGetMethod(true) != null)
                return property;

            FieldInfo field = type.GetField(name, Flags);
            if (field != null)
                return field;

            return type.GetField("<" + name + ">k__BackingField", Flags);
        }

        private static void EnsureResolved()
        {
            if (_resolved) return;
            _resolved = true;

            try
            {
                Assembly xpAssembly = null;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    AssemblyName name = assembly.GetName();
                    if (name != null && string.Equals(name.Name, "XPerfect", StringComparison.OrdinalIgnoreCase))
                    {
                        xpAssembly = assembly;
                        break;
                    }
                }

                if (xpAssembly == null)
                    return;

                _accuracyStateType = xpAssembly.GetType("XPerfect.AccuracyState");
                if (_accuracyStateType == null)
                    return;

                _lastJudgeMember = GetStaticReadable(_accuracyStateType, "LastJudge");
                _lastJudgeForTextMember = GetStaticReadable(_accuracyStateType, "LastJudgeForText");
                _xCountMember = GetStaticReadable(_accuracyStateType, "XPerfectCount");
                _plusCountMember = GetStaticReadable(_accuracyStateType, "PlusPerfectCount");
                _minusCountMember = GetStaticReadable(_accuracyStateType, "MinusPerfectCount");

                Type mainType = xpAssembly.GetType("XPerfect.Main");
                if (mainType != null)
                {
                    _enabledProperty = mainType.GetProperty("Enabled", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                }

                _installed = _xCountMember != null
                    && _plusCountMember != null
                    && _minusCountMember != null;
            }
            catch
            {
                _installed = false;
            }
        }
    }
}
