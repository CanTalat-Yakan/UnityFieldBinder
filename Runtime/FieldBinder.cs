using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UnityEssentials
{
    public class FieldBinder : MonoBehaviour
    {
        public enum BindingDirection { AtoB, BtoA, TwoWay }

        public Object objectA;
        public Object objectB;
        public string pathA;
        public string pathB;
        [Enum(nameof(PathsA))]
        public string selectedPath;
        public string[] PathsA = new string[] { "Test", "Test2", "Test3" };
        public BindingDirection direction = BindingDirection.AtoB;

        private static List<FieldBinder> _registeredBinders = new();

        public void Awake() =>
            _registeredBinders.Add(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InitBindings()
        {
            foreach (var binder in _registeredBinders)
                binder?.ApplyBinding();
        }

        public void ApplyBinding()
        {
            if (!objectA || !objectB) 
                return;

            var valueA = GetValue(objectA, pathA);
            var valueB = GetValue(objectB, pathB);

            switch (direction)
            {
                case BindingDirection.AtoB:
                    SetValue(objectB, pathB, valueA);
                    break;
                case BindingDirection.BtoA:
                    SetValue(objectA, pathA, valueB);
                    break;
                case BindingDirection.TwoWay:
                    if (valueA != null && !valueA.Equals(valueB))
                        SetValue(objectB, pathB, valueA);
                    else if (valueB != null && !valueB.Equals(valueA))
                        SetValue(objectA, pathA, valueB);
                    break;
            }
        }

        object GetValue(Object source, string path)
        {
            var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var type = source.GetType();
            var member = type.GetField(path, bindingFlags) ?? (MemberInfo)type.GetProperty(path, bindingFlags);

            if (member is FieldInfo field) 
                return field.GetValue(source);
            if (member is PropertyInfo prop) 
                return prop.GetValue(source);

            return null;
        }

        void SetValue(Object source, string path, object value)
        {
            var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var type = source.GetType();
            var field = type.GetField(path, bindingFlags);
            if (field != null && field.FieldType.IsInstanceOfType(value))
            {
                field.SetValue(source, value);
                return;
            }

            var property = type.GetProperty(path, bindingFlags);
            if (property != null && property.CanWrite && property.PropertyType.IsInstanceOfType(value))
                property.SetValue(source, value);
        }
    }
}
