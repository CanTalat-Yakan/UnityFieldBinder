using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UnityEssentials
{
    [ExecuteAlways]
    public class FieldBinder : MonoBehaviour
    {
        public static List<FieldBinder> RegisteredBinders { get; private set; } = new();

        [Info(MessageType.Warning)]
        public string Info = string.Empty;

        public enum BindingDirection { OneWayAB, OneWayBA, TwoWay }
        public BindingDirection direction = BindingDirection.OneWayAB;

        [Space]
        public Object SourceA;
        [Enum(nameof(_dynamicReferencesA))]
        public string ReferenceA;

        [Space]
        public Object SourceB;
        [Enum(nameof(_dynamicReferencesB))]
        public string ReferenceB;

        private string[] _dynamicReferencesA;
        private string[] _dynamicReferencesB;

        private bool _showAllReferences = false;

        private static BindingFlags s_bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public void Awake()
        {
            RegisteredBinders.Add(this);

            OnSourceAChanged();
            OnSourceBChanged();
        }

        [OnValueChanged(nameof(SourceA))]
        public void OnSourceAChanged() =>
            FetchReferences(SourceA, out _dynamicReferencesA);

        [OnValueChanged(nameof(SourceB))]
        public void OnSourceBChanged() =>
            FetchReferences(SourceB, out _dynamicReferencesB);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void InitializeBindings()
        {
            foreach (var binder in RegisteredBinders)
                binder?.ApplyBinding();
        }

        [ContextMenu("Show All References")]
        public void ShowAllReferences()
        {
            _showAllReferences = !_showAllReferences;

            OnSourceAChanged();
            OnSourceBChanged();
        }

        public void ApplyBinding()
        {
            if (!SourceA || !SourceB)
                return;

            OnSourceAChanged();
            OnSourceBChanged();

            var valueA = GetValue(SourceA, ReferenceA);
            var valueB = GetValue(SourceB, ReferenceB);

            switch (direction)
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

        private object GetValue(Object source, string path)
        {
            var type = source.GetType();

            var field = type.GetField(path, s_bindingFlags);
            if (field != null)
                return field.GetValue(source);

            var property = type.GetProperty(path, s_bindingFlags);
            if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
                return property.GetValue(source);

            return null;
        }

        private void SetValue(Object source, string path, object value)
        {
            var sourceType = source.GetType();

            var field = sourceType.GetField(path, s_bindingFlags);
            if (field != null && field.FieldType.IsInstanceOfType(value))
            {
                field.SetValue(source, value);
                return;
            }

            var property = sourceType.GetProperty(path, s_bindingFlags);
            if (property != null && property.CanWrite && property.PropertyType.IsInstanceOfType(value) && property.GetIndexParameters().Length == 0)
            {
                property.SetValue(source, value);
            }
        }

        private void FetchReferences(object source, out string[] dynamicReferences)
        {
            if (source == null)
            {
                dynamicReferences = System.Array.Empty<string>();
                return;
            }

            var sourceType = source.GetType();
            var typeChain = new List<System.Type>();

            // Collect all types up to (but not including) MonoBehaviour, from base to derived
            while (sourceType != null && sourceType != typeof(MonoBehaviour))
            {
                typeChain.Insert(0, sourceType); // Insert at the beginning to reverse the order
                sourceType = sourceType.BaseType;
            }

            var fieldNames = new List<string>();

            foreach (var typeInChain in typeChain)
            {
                BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;

                if (_showAllReferences)
                    bindingFlags |= BindingFlags.NonPublic;

                foreach (var field in typeInChain.GetFields(bindingFlags))
                    if (!field.IsSpecialName && !field.Name.StartsWith("<"))
                        fieldNames.Add(field.Name);

                if (_showAllReferences)
                    foreach (var property in typeInChain.GetProperties(bindingFlags))
                        if (property.CanRead && property.GetIndexParameters().Length == 0)
                            fieldNames.Add(property.Name);
            }

            dynamicReferences = fieldNames.ToArray();
        }
    }
}
