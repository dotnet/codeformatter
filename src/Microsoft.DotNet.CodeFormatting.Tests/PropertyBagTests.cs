// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CodeAnalysis.Options
{
    [TestClass]
    public class PropertyBagTests
    {
        [TestMethod]
        public void PropertyBag_TryConvertFromString()
        {
            var propertyBag = new PropertyBag();
            propertyBag["TestValue"] = "15";
            int converted;
            Assert.IsTrue(propertyBag.TryGetProperty("TestValue", out converted));
            Assert.AreEqual(15, converted, "String conversion to primitive type did not succeed.");
        }

        [TestMethod]
        public void PropertyBag_InitializeFromPropertyBag()
        {
            int expectedValue = 72;
            var propertyBag = new PropertyBag();
            propertyBag["TestValue"] = expectedValue.ToString();
            
            var copiedPropertyBag = new PropertyBag(propertyBag, StringComparer.OrdinalIgnoreCase);

            int converted;
            Assert.IsTrue(propertyBag.TryGetProperty("TestValue", out converted));
            Assert.AreEqual(expectedValue, converted, "String conversion to primitive type did not succeed.");

            Assert.IsTrue(propertyBag.TryGetProperty("testvalue", out converted));
            Assert.AreEqual(expectedValue, converted, "String conversion to primitive type did not succeed.");
        }

        [TestMethod]
        public void PropertyBag_SetProperty()
        {
            var propertyBag = new PropertyBag();

            var descriptor = new PerLanguageOption<string>("feature", "name", "defaultValue");

            propertyBag.SetProperty(descriptor, "value");
            Assert.IsTrue(propertyBag.GetProperty(descriptor) == "value");

            propertyBag.SetProperty(descriptor, null);
            Assert.IsTrue(propertyBag.GetProperty(descriptor) == "defaultValue");
        }

        [TestMethod]
        public void PropertyBag_BinarySerialization()
        {
            var testData = new TestData();
            var propertyBag = new PropertyBag();
            testData.InitializePropertyBag(propertyBag);


            var formatter = new BinaryFormatter();

            string path = Path.GetTempFileName();

            try
            {
                using (var writer = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    formatter.Serialize(writer, propertyBag);
                }

                using (var reader = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    propertyBag = (PropertyBag)formatter.Deserialize(reader);
                    testData.ValidatePropertyBag(propertyBag);
                }
            }
            finally
            {
                try { File.Delete(path); } catch (IOException) { }
            }
        }

        [TestMethod]
        public void PropertyBag_ArgumentNullChecks()
        {
            bool argumentNull = false;

            var propertyBag = new PropertyBag();
            try
            {
                propertyBag.SetProperty((PerLanguageOption<string>)null, "string");
            }
            catch (ArgumentNullException) { argumentNull = true; }
            Assert.IsTrue(argumentNull);

            argumentNull = false;
            try
            {
                propertyBag.GetProperty((PerLanguageOption<string>)null);
            }
            catch (ArgumentNullException) { argumentNull = true; }
            Assert.IsTrue(argumentNull);
        }

        [TestMethod]
        public void PropertyBag_HumanReadableSerialization()
        {
            TestData testData = new TestData();
            var propertyBag = new PropertyBag();
            testData.InitializePropertyBag(propertyBag);

            string path = Path.GetTempFileName();

            try
            {
                using (var writer = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    propertyBag.SaveTo(writer, "TestProperties");
                }

                // reset to empty property bag
                propertyBag = new PropertyBag();

                using (var reader = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    propertyBag.LoadFrom(reader);
                    testData.ValidatePropertyBag(propertyBag);
                }

                propertyBag.SaveTo(path, "PropertiesId");

                // reset to empty property bag
                propertyBag = new PropertyBag();
                propertyBag.LoadFrom(path);
                testData.ValidatePropertyBag(propertyBag);
            }
            finally
            {
                try { File.Delete(path); } catch (IOException) { }
            }
        }

        class TestData
        {
            public bool BooleanValue;
            public double DoubleValue;
            public TestEnum TestEnumValue;
            public StringSet StringSetValue;

            public bool EmbeddedBooleanValue;
            public double EmbeddedDoubleValue;
            public TestEnum EmbeddedTestEnumValue;
            public StringSet EmbeddedStringSetValue;

            public PropertyBag PropertyBagValue;
            public TestEnumPropertyBag TypedPropertyBagValue;

            public TestData()
            {
                BooleanValue = true;
                EmbeddedBooleanValue = false;

                DoubleValue = Double.MaxValue;
                EmbeddedDoubleValue = Double.MinValue;

                TestEnumValue = TestEnum.ValueTwo;
                EmbeddedTestEnumValue = TestEnum.ValueTwo;

                StringSetValue = new StringSet(new string[] { "v1", "v2" });
                EmbeddedStringSetValue = new StringSet(new string[] { "v3", "v4" });
            }

            private void ValidatePropertyBag(IDictionary expected, IDictionary actual)
            {
                Assert.AreEqual(expected.Count, actual.Count);

                foreach (string key in expected.Keys)
                {
                    Assert.IsTrue(actual.Contains(key));
                    IDictionary expectedDictionary = expected[key] as IDictionary;

                    if (expectedDictionary != null)
                    {
                        ValidatePropertyBag(expectedDictionary, (IDictionary)actual[key]);
                        continue;
                    }

                    StringSet expectedStringSet = expected[key] as StringSet;
                    if (expectedStringSet != null)
                    {
                        ValidateStringSet(expectedStringSet, (StringSet)actual[key]);
                        continue;
                    }
                    Assert.AreEqual(expected[key], actual[key]);
                }
            }

            private static void ValidateStringSet(StringSet expected, StringSet actual)
            {
                Assert.AreEqual(expected.Count, actual.Count);
                foreach (string value in expected)
                {
                    Assert.IsTrue(actual.Contains(value));
                }
            }

            internal void InitializePropertyBag(PropertyBag propertyBag)
            {
                propertyBag.SetProperty(DoubleOption, DoubleValue);
                propertyBag.SetProperty(BooleanOption, BooleanValue);
                propertyBag.SetProperty(TestEnumOption, TestEnumValue);
                propertyBag.SetProperty(StringSetOption, StringSetValue);

                PropertyBagValue = new PropertyBag();
                PropertyBagValue.SetProperty(DoubleOption, EmbeddedDoubleValue);
                PropertyBagValue.SetProperty(BooleanOption, EmbeddedBooleanValue);
                PropertyBagValue.SetProperty(TestEnumOption, EmbeddedTestEnumValue);
                PropertyBagValue.SetProperty(StringSetOption, EmbeddedStringSetValue);


                propertyBag.SetProperty(PropertyBagOption, PropertyBagValue);

                TypedPropertyBagValue = propertyBag.GetProperty(TypedPropertyBagOption);
                TypedPropertyBagValue.SetProperty(TestEnumOption, EmbeddedTestEnumValue);

                propertyBag.SetProperty(TypedPropertyBagOption, TypedPropertyBagValue);
            }

            internal void ValidatePropertyBag(PropertyBag propertyBag)
            {
                Assert.AreEqual(DoubleValue, propertyBag.GetProperty(DoubleOption));
                Assert.AreEqual(BooleanValue, propertyBag.GetProperty(BooleanOption));
                Assert.AreEqual(TestEnumValue, propertyBag.GetProperty(TestEnumOption));

                ValidateStringSet(StringSetValue, propertyBag.GetProperty(StringSetOption));

                Assert.AreEqual(EmbeddedDoubleValue, propertyBag.GetProperty(PropertyBagOption).GetProperty(DoubleOption));
                Assert.AreEqual(EmbeddedBooleanValue, propertyBag.GetProperty(PropertyBagOption).GetProperty(BooleanOption));
                Assert.AreEqual(EmbeddedTestEnumValue, propertyBag.GetProperty(PropertyBagOption).GetProperty(TestEnumOption));

                ValidateStringSet(EmbeddedStringSetValue, propertyBag.GetProperty(PropertyBagOption).GetProperty(StringSetOption));

                ValidatePropertyBag(PropertyBagValue, propertyBag.GetProperty(PropertyBagOption));
                ValidatePropertyBag(TypedPropertyBagValue, propertyBag.GetProperty(TypedPropertyBagOption));
            }
        }

        private static PerLanguageOption<bool> BooleanOption = 
            new PerLanguageOption<bool>("PropertyBagTests", "BooleanSetting", false);

        private static PerLanguageOption<double> DoubleOption = 
            new PerLanguageOption<double>("PropertyBagTests", "DoubleSetting", 4.7d);

        private static PerLanguageOption<PropertyBag> PropertyBagOption = 
            new PerLanguageOption<PropertyBag>("PropertyBagTests", "PropertyBagSetting", new PropertyBag());

        private static PerLanguageOption<TestEnum> TestEnumOption =
            new PerLanguageOption<TestEnum>("PropertyBagTests", "TestEnumSetting", TestEnum.ValueThree);

        private static PerLanguageOption<StringSet> StringSetOption =
            new PerLanguageOption<StringSet>("PropertyBagTests", "StringSetSetting", new StringSet(new string[] { "one", "two" }));

        [Serializable]
        enum TestEnum { ValueOne = 1, ValueTwo, ValueThree }

        private static PerLanguageOption<TestEnumPropertyBag> TypedPropertyBagOption =
            new PerLanguageOption<TestEnumPropertyBag>("PropertyBagTests", "TypePropertyBagSetting", new TestEnumPropertyBag());

        [Serializable]
        class TestEnumPropertyBag : TypedPropertyBag<TestEnum>
        {
            public TestEnumPropertyBag() { }

            protected TestEnumPropertyBag(SerializationInfo info, StreamingContext context)
            : base(info, context)
            {
            }
        }
    }
}
