// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;

namespace Microsoft.CodeAnalysis.Options
{
    [Serializable]
    public class PropertyBag : TypedPropertyBag<object>
    {
        public PropertyBag() : base() { }

        public PropertyBag(PropertyBag initializer, IEqualityComparer<string> comparer)
            : base(initializer, comparer)
        {
        }

        protected PropertyBag(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        internal string Name { get; set; }

        public virtual T GetProperty<T>(PerLanguageOption<T> setting, bool cacheDefault = true)
        {
            if (setting == null) { throw new ArgumentNullException("setting"); }

            PropertyBag properties = GetSettingsContainer(setting);

            T value;
            if (!properties.TryGetProperty(setting.Name, out value) && setting.DefaultValue != null)
            {
                value = setting.DefaultValue;

                if (cacheDefault) { properties[setting.Name] = value; }
            }
            return value;
        }

        public override void SetProperty(IOption setting, object value)
        {
            if (setting == null) { throw new ArgumentNullException("setting"); }

            PropertyBag properties = GetSettingsContainer(setting);

            if (value == null && properties.ContainsKey(setting.Name))
            {
                properties.Remove(setting.Name);
                return;
            }
            properties[setting.Name] = value;
        }

        internal bool TryGetProperty<T>(string key, out T value)
        {
            value = default(T);

            object result;
            if (this.TryGetValue(key, out result))
            {
                if (result is T)
                {
                    value = (T)result;
                    return true;
                }
                return TryConvertFromString((string)result, out value);
            }

            return false;
        }

        private PropertyBag GetSettingsContainer(IOption setting)
        {
            PropertyBag properties = this;

            if (String.IsNullOrEmpty(Name))
            {
                object propertiesObject;
                if (!TryGetValue(setting.Feature, out propertiesObject))
                {
                    this[setting.Feature] = properties = new PropertyBag();
                    properties.Name = setting.Feature;
                }
                else
                {
                    properties = (PropertyBag)propertiesObject;
                }
            }
            return properties;
        }

        private static bool TryConvertFromString<T>(string source, out T destination)
        {
            destination = default(T);
            if (source == null) return false;
            TypeConverter converter = TypeDescriptor.GetConverter(typeof(T));
            if (converter == null) return false;
            destination = (T)converter.ConvertFrom(source);
            return destination != null;
        }

        public void SaveTo(string filePath, string id)
        {
            if (filePath == null)
                return;

            using (var writer = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                SaveTo(writer, id);
        }

        public void SaveTo(Stream stream, string id)
        {
            var settings = new XmlWriterSettings { Indent = true };
            using (XmlWriter writer = XmlWriter.Create(stream, settings))
            {
                this.SavePropertyBagToStream(writer, settings, id);
            }
        }

        public void LoadFrom(string filePath)
        {
            using (var reader = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                LoadFrom(reader);
        }

        public void LoadFrom(Stream stream)
        {
            if (stream == null || stream.Length <= 3)
                return;

            using (XmlReader reader = XmlReader.Create(stream))
            {
                if (reader.IsStartElement(PropertyBagExtensionMethods.PROPERTIES_ID))
                {
                    this.Clear();
                    
                    // Note: we do not recover the property bag id
                    //       as there is no current product use for the value

                    reader.ReadStartElement(PropertyBagExtensionMethods.PROPERTIES_ID);
                    this.LoadPropertiesFromXmlStream(reader);
                    reader.ReadEndElement();
                }
            }
        }

        // Current consumers of this data expect that child namespaces
        // will always precede parent namespaces, if also included.
        public static ImmutableArray<string> DefaultNamespaces = new List<string>(
            new string[] {
                "Microsoft.CodeAnalysis.Options.",
                "Microsoft.CodeAnalysis."
            }).ToImmutableArray();
    }
}
