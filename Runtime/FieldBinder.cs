using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UnityEssentials
{
    [ExecuteAlways]
    public class FieldBinder : MonoBehaviour
    {
        public static List<FieldBinder> RegisteredBinders { get; } = new();

        [Info(MessageType.Warning)] private string _info = string.Empty;

        public enum BindingDirection { OneWayAB, OneWayBA, TwoWay }
        public BindingDirection Direction = BindingDirection.OneWayAB;

        [Space]
        public UnityEngine.Object SourceA;
        [Enum(nameof(_referencesA))] public string ReferenceA;

        [Space]
        public UnityEngine.Object SourceB;
        [Enum(nameof(_referencesB))] public string ReferenceB;

        private string[] _referencesA, _referencesB;
        private bool _showAllReferences = false;

        private static readonly BindingFlags BindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public void Awake()
        {
            RegisteredBinders.Add(this);
            OnBindingValueChange();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void InitializeBindings() =>
            RegisteredBinders.ForEach(b => b?.ApplyBinding());

        [OnValueChanged(nameof(Direction), nameof(SourceA), nameof(SourceB), nameof(ReferenceA), nameof(ReferenceB))]
        public void OnBindingValueChange()
        {
            _referencesA = CollectReferences(SourceA);
            _referencesB = CollectReferences(SourceB);
            ValidateTypes();
        }

        [ContextMenu("Show All References")]
        public void ToggleShowAll()
        {
            _showAllReferences = !_showAllReferences;
            OnBindingValueChange();
        }

        public void ApplyBinding()
        {
            if (!SourceA || !SourceB)
                return;

            OnBindingValueChange();

            var valueA = GetValue(SourceA, ReferenceA);
            var valueB = GetValue(SourceB, ReferenceB);

            switch (Direction)
            {
                case BindingDirection.OneWayAB:
                    SetValue(SourceB, ReferenceB, valueA);
                    break;
                case BindingDirection.OneWayBA:
                    SetValue(SourceA, ReferenceA, valueB);
                    break;
                case BindingDirection.TwoWay:
                    if (valueA != null && !valueA.Equals(valueB))
                        SetValue(SourceB, ReferenceB, valueA);
                    else if (valueB != null && !valueB.Equals(valueA))
                        SetValue(SourceA, ReferenceA, valueB);
                    break;
            }
        }

        private object GetValue(UnityEngine.Object source, string path)
        {
            var type = source.GetType();

            var field = type.GetField(path, BindingFlags);
            if (field != null)
                return field.GetValue(source);

            var property = type.GetProperty(path, BindingFlags);
            if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
                return property.GetValue(source);

            return null;
        }

        private void SetValue(UnityEngine.Object source, string path, object value)
        {
            var type = source.GetType();

            var field = type.GetField(path, BindingFlags);
            if (field != null && field.FieldType.IsInstanceOfType(value))
            {
                field.SetValue(source, value);
                return;
            }

            var property = type.GetProperty(path, BindingFlags);
            if (property != null && property.PropertyType.IsInstanceOfType(value) && property.CanWrite && property.GetIndexParameters().Length == 0)
                property.SetValue(source, value);
        }

        private string[] CollectReferences(object source)
        {
            if (source == null)
                return Array.Empty<string>();

            var type = source.GetType();
            var chain = new List<Type>();

            while (type != null && type != typeof(MonoBehaviour))
            {
                chain.Insert(0, type);
                type = type.BaseType;
            }

            var references = new List<string>();

            foreach (var typeInChain in chain)
            {
                var bindingflags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;
                if (_showAllReferences)
                    bindingflags |= BindingFlags.NonPublic;

                foreach (var field in typeInChain.GetFields(bindingflags))
                {
                    if (field.IsSpecialName || field.Name.StartsWith("<"))
                        continue;

                    references.Add(field.Name);
                    AddNested(field.FieldType, field.Name, references, 1);
                }

                if (!_showAllReferences)
                    continue;

                foreach (var property in typeInChain.GetProperties(bindingflags))
                {
                    if (!property.CanRead || property.GetIndexParameters().Length != 0)
                        continue;

                    references.Add(property.Name);
                    AddNested(property.PropertyType, property.Name, references, 1);
                }
            }

            return references.ToArray();
        }

        private void AddNested(Type type, string prefix, List<string> output, int depth)
        {
            if (depth > 3 || type == null || type.IsPrimitive || type.IsEnum || type.IsArray || type.IsGenericType)
                return;

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return;

            if (!type.IsSerializable && type.GetCustomAttribute<SerializableAttribute>() == null)
                return;

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;

            foreach (var field in type.GetFields(flags))
            {
                if (field.IsSpecialName || field.Name.StartsWith("<"))
                    continue;

                var name = $"{prefix}.{field.Name}";
                output.Add(name);
                AddNested(field.FieldType, name, output, depth + 1);
            }

            if (!_showAllReferences)
                return;

            foreach (var property in type.GetProperties(flags))
            {
                if (type == typeof(string) && property.Name == "Length")
                    continue;

                if (!property.CanRead || property.GetIndexParameters().Length != 0)
                    continue;

                var name = $"{prefix}.{property.Name}";
                output.Add(name);
                AddNested(property.PropertyType, name, output, depth + 1);
            }
        }

        private void ValidateTypes()
        {
            _info = string.Empty;

            if (!SourceA || !SourceB || string.IsNullOrEmpty(ReferenceA) || string.IsNullOrEmpty(ReferenceB))
                return;

            var typeA = ResolvePathType(SourceA.GetType(), ReferenceA);
            var typeB = ResolvePathType(SourceB.GetType(), ReferenceB);

            if (typeA == null || typeB == null)
                _info = "Could not resolve types for selected references.";
            else if (typeA != typeB)
                _info = Direction switch
                {
                    BindingDirection.OneWayAB => $"Cannot bind {typeA.Name} to {typeB.Name}. Types must be exactly the same (A → B).",
                    BindingDirection.OneWayBA => $"Cannot bind {typeB.Name} to {typeA.Name}. Types must be exactly the same (B → A).",
                    _ => $"Cannot bind {typeA.Name} and {typeB.Name}. Types must be exactly the same (TwoWay)."
                };
        }

        private Type ResolvePathType(Type type, string path)
        {
            foreach (var part in path.Split('.'))
            {
                var field = type.GetField(part, BindingFlags);
                if (field != null)
                {
                    type = field.FieldType;
                    continue;
                }

                var property = type.GetProperty(part, BindingFlags);
                if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
                {
                    if (type == typeof(string) && property.Name == "Length")
                        return null;

                    type = property.PropertyType;
                    continue;
                }

                return null;
            }
            return type;
        }
    }
}