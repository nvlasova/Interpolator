﻿/*
 * SerializableGenerics
 * Copyright (c) 2009, Eduardo Sanchez-Ros
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, version 3 of the License.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

//http://eduardosanchezros.blogspot.ru/2011/03/serializing-generics_3120.html

using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Xml.Schema;
using System.Xml;

namespace SerializableGenerics
{
    public static class SerializableGenerics
    {
        private const string OF = "Of";

        /// <summary>
        /// Gets the key-value pair name
        /// </summary>
        /// <param name="tKey"></param>
        /// <param name="tValue"></param>
        /// <returns></returns>
        public static string GetKeyValuePairName(Type tKey, Type tValue)
        {
            // return key-value pair name
            return GetTypeName(tKey) + GetTypeName(tValue);
        }

        /// <summary>
        /// Returns the name of the type
        /// </summary>
        /// <param name="type">Type to generate the name from</param>
        /// <returns>The name of the type</returns>
        public static String GetTypeName(Type type)
        {
            String typeName;

            // Is type generic
            if (type.IsGenericType)
            {
                // Get type name - Generics 
                typeName = type.Name.Substring(0, type.Name.Length - 2);
                typeName += OF;

                // Get type's arguments
                Type[] types = type.GetGenericArguments();
                foreach (Type t in types)
                {
                    // Append type's name
                    typeName += GetTypeName(t);
                }
            }
            else if (type.IsArray)
            {
                // Compose array name as "ArrayOf" and get array element's type name
                typeName = type.BaseType.Name;
                typeName += OF;
                typeName += GetTypeName(type.GetElementType());
            }
            else
            {
                // Append type's name
                typeName = type.Name;
            }

            // return type's name
            return typeName;
        }
    }

    /// <summary>
    /// Represents a serializable collection of key/value pairs that are sorted by key based on
    ///     the associated System.Collections.Generic.IComparer<T> implementation.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys on the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values on the dictionary.</typeparam>
    public class SerializableSortedList<TKey, TValue> : SortedList<TKey, TValue>, IXmlSerializable
    {
        // store key and value types
        private readonly Type m_tKey = typeof(TKey);
        private readonly Type m_tValue = typeof(TValue);

        /// <summary>
        /// Returns a string that represents the current SerializableDictionary.
        /// </summary>
        /// <returns>
        /// A string that represents the current SerializableDictionary.
        /// </returns>
        public override string ToString()
        {
            return SerializableGenerics.GetTypeName(GetType());
        }

        #region IXmlSerializable Members

        /// <summary>
        /// This property is reserved, apply the System.Xml.Serialization.XmlSchemaProviderAttribute
        /// to the class instead.
        /// </summary>
        /// <returns>
        /// An System.Xml.Schema.XmlSchema that describes the XML representation of the
        /// object that is produced by the System.Xml.Serialization.IXmlSerializable.WriteXml(System.Xml.XmlWriter)
        /// method and consumed by the System.Xml.Serialization.IXmlSerializable.ReadXml(System.Xml.XmlReader)
        /// method.
        /// </returns>
        XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        /// <summary>
        /// Generates an object from its XML representation.
        /// </summary>
        /// <param name="reader">The System.Xml.XmlReader stream from which the object is deserialized.</param>
        void IXmlSerializable.ReadXml(XmlReader reader)
        {
            // Create xml serializers for key and value
            XmlSerializer keySerializer = new XmlSerializer(m_tKey);
            XmlSerializer valueSerializer = new XmlSerializer(m_tValue);

            // Get key-value pair name
            string keyValuePairName = SerializableGenerics.GetKeyValuePairName(m_tKey, m_tValue);

            // Read start element and move to content
            reader.ReadStartElement();
            reader.MoveToContent();

            // Is keyValuePairName the start element
            if (!reader.IsStartElement(keyValuePairName))
            {
                // Throw an exception
                throw new XmlException("Starting element " + keyValuePairName + " not found.");
            }

            // Loop through key-value pairs
            while (reader.IsStartElement(keyValuePairName))
            {
                // Read key-value pair and move to content
                reader.ReadStartElement(keyValuePairName);
                reader.MoveToContent();

                // Deserialize key and value
                TKey key = (TKey)keySerializer.Deserialize(reader);
                TValue value = (TValue)valueSerializer.Deserialize(reader);

                // Read end element and add key-value pair to dictionary
                reader.ReadEndElement();
                Add(key, value);
            }

            // Read end element and move to content
            reader.ReadEndElement();
            reader.MoveToContent();
        }

        /// <summary>
        /// Converts an object into its XML representation.
        /// </summary>
        /// <param name="writer">The System.Xml.XmlWriter stream to which the object is serialized.</param>
        void IXmlSerializable.WriteXml(XmlWriter writer)
        {
            // Create xml serializers for key and value
            XmlSerializer keySerializer = new XmlSerializer(m_tKey);
            XmlSerializer valueSerializer = new XmlSerializer(m_tValue);

            // Get key-value pair name
            string keyValuePairName = SerializableGenerics.GetKeyValuePairName(m_tKey, m_tValue);

            IEnumerator<KeyValuePair<TKey, TValue>> enumerator = GetEnumerator();
            while (enumerator.MoveNext())
            {
                // Get current key value pair
                KeyValuePair<TKey, TValue> keyValuePair = enumerator.Current;

                // Write start element with key-value pair name 
                writer.WriteStartElement(keyValuePairName);

                // Serialize key and value
                keySerializer.Serialize(writer, keyValuePair.Key);
                valueSerializer.Serialize(writer, keyValuePair.Value);

                // Write end element with key-value pair name
                writer.WriteEndElement();
            }
        }

        #endregion
    }
}
