// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Xml;

namespace Microsoft.CodeAnalysis.Options
{
    public static class PropertyBagExtensionMethods
    {
        public static void SavePropertyBagToStream(this IDictionary propertyBag, XmlWriter writer, XmlWriterSettings settings, string name)
        {
            Type propertyBagType;
            string propertyBagTypeName;

            propertyBagType = propertyBag.GetType();
            propertyBagTypeName = propertyBagType.Name;

            if (propertyBagTypeName != "PropertyBag")
            {
                propertyBagTypeName = NormalizeTypeName(propertyBag.GetType().FullName);
            }

            writer.WriteStartElement(PROPERTIES_ID);
            writer.WriteAttributeString(KEY_ID, name);

            if (propertyBagTypeName != "PropertyBag")
            {
                writer.WriteAttributeString(TYPE_ID, propertyBagTypeName);
            }

            string[] tokens = new string[propertyBag.Keys.Count];
            propertyBag.Keys.CopyTo(tokens, 0);

            Array.Sort<string>(tokens);

            foreach (string key in tokens)
            {
                object property = propertyBag[key];                

                StringSet stringSet = property as StringSet;
                if (stringSet != null)
                {
                    SaveStringSet(writer, stringSet, key);
                    continue;
                }

                IDictionary pb = property as IDictionary;
                if (pb != null)
                {
                    ((IDictionary)pb).SavePropertyBagToStream(writer, settings, key);
                    continue;
                }

                writer.WriteStartElement(PROPERTY_ID);
                writer.WriteAttributeString(KEY_ID, key);

                Type propertyType = property.GetType();
                TypeConverter tc = TypeDescriptor.GetConverter(propertyType);
                writer.WriteAttributeString(VALUE_ID, tc.ConvertToString(property));

                if (propertyType != typeof(string))
                {
                    string typeName = NormalizeTypeName(propertyType.FullName);
                    writer.WriteAttributeString(TYPE_ID, typeName);
                }

                writer.WriteEndElement(); // KeyValuePair
            }
            writer.WriteEndElement(); // Properties
        }

        private static string NormalizeTypeName(string typeName)
        {
            // This helper currently assumes that namespaces in 
            // DefaultNamespaces will be sorted such that parent
            // namespaces, if they are included, will always
            // be listed after children. We could remove this
            // requirement by changing this code to process all
            // namespaces and to select the shortest type name
            // that results.                        
            foreach (string nsPrefix in PropertyBag.DefaultNamespaces)
            {
                if (typeName.StartsWith(nsPrefix, StringComparison.Ordinal))
                {
                    return typeName.Substring(nsPrefix.Length);
                }
            }
            return typeName;
        }

        private static void SaveStringSet(XmlWriter writer, StringSet items, string key)
        {
            writer.WriteStartElement(PROPERTY_ID);
            writer.WriteAttributeString(KEY_ID, key);
            writer.WriteAttributeString(TYPE_ID, "StringSet");

            string[] sorted = new string[items.Count];
            items.CopyTo(sorted, 0);
            Array.Sort(sorted);

            foreach (string item in sorted)
            {
                writer.WriteStartElement(ITEM_ID);
                writer.WriteString(item);
                writer.WriteEndElement(); // Item
            }

            writer.WriteEndElement(); // Property
        }

        #region Human-readable serialization logic

        public static void LoadPropertiesFromXmlStream(this IDictionary propertyBag, XmlReader reader)
        {
            while (reader.IsStartElement(PROPERTIES_ID) || reader.IsStartElement(PROPERTY_ID))
            {
                string key = null;
                string value = null;
                bool isEmpty;

                if (reader.IsStartElement(PROPERTIES_ID))
                {
                    key = reader.GetAttribute(KEY_ID);

                    string typeName = reader.GetAttribute(TYPE_ID);

                    IDictionary nestedPropertyBag;

                    if (String.IsNullOrEmpty(typeName))
                    {
                        nestedPropertyBag = new PropertyBag();
                    }
                    else
                    {
                        Type type = GetPropertyBagType(typeName);
                        nestedPropertyBag = (IDictionary)Activator.CreateInstance(type);
                    }

                    propertyBag[key] = nestedPropertyBag;
                    isEmpty = reader.IsEmptyElement;
                    reader.ReadStartElement(PROPERTIES_ID);
                    LoadPropertiesFromXmlStream(nestedPropertyBag, reader);
                    if (!isEmpty) { reader.ReadEndElement(); }
                }
                else
                {
                    key = reader.GetAttribute(KEY_ID);
                    value = reader.GetAttribute(VALUE_ID);
                    string typeName = reader.GetAttribute(TYPE_ID);
                    isEmpty = reader.IsEmptyElement;

                    if (typeName == STRING_SET_ID)
                    {
                        StringSet set = new StringSet();
                        propertyBag[key] = set;
                        LoadStringSet(set, reader);
                        if (!isEmpty) { reader.ReadEndElement(); }
                        continue;
                    }

                    reader.ReadStartElement(PROPERTY_ID);
                    Type propertyType = GetPropertyBagType(typeName);
                    TypeConverter tc = TypeDescriptor.GetConverter(propertyType);

                    Debug.Assert(tc.CanConvertFrom(typeof(string)));

                    object propertyValue = tc.ConvertFromString(value);
                    propertyBag[key] = propertyValue;

                    Debug.Assert(isEmpty);
                }
            }
        }

        private static Type GetPropertyBagType(string typeName)
        {
            Type type = GetType(typeName);

            if (type == null)
            {
                foreach (string nsPrefix in PropertyBag.DefaultNamespaces)
                {
                    type = GetType(nsPrefix + typeName);
                    if (type != null) { break; }
                }
            }

            return type;
        }
        
        private static void LoadStringSet(StringSet set, XmlReader reader)
        {
            reader.ReadStartElement(PROPERTY_ID);

            while (reader.IsStartElement(ITEM_ID))
            {
                string item;
                bool isEmptyItem;

                isEmptyItem = reader.IsEmptyElement;
                reader.ReadStartElement();
                item = reader.ReadString();
                set.Add(item);
                if (!isEmptyItem) reader.ReadEndElement();
            }
        }

        private static Type GetType(string typeName)
        {
            Type propertyType = null;
            if (!s_typesCache.Contains(typeName))
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    propertyType = assembly.GetType(typeName);

                    if (propertyType != null)
                    {
                        s_typesCache[typeName] = propertyType;
                        break;
                    }
                }
            }
            else
            {
                propertyType = (Type)s_typesCache[typeName];
            }
            return propertyType;
        }


        #endregion

        private const string KEY_ID = "Key";
        private const string SET_ID = "Set";
        private const string ITEM_ID = "Item";
        private const string TYPE_ID = "Type";
        private const string VALUE_ID = "Value";
        private const string PROPERTY_ID = "Property";
        private const string ITEMTYPE_ID = "ItemType";
        private const string STRING_SET_ID = "StringSet";
        internal const string PROPERTIES_ID = "Properties";

        private static HybridDictionary s_typesCache = new HybridDictionary();
    }
}
