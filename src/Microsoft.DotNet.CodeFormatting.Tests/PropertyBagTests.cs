// /********************************************************
// *                                                       *
// *   Copyright (C) Microsoft. All rights reserved.       *
// *                                                       *
// ********************************************************/

using System;
using System.Collections;
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
        public void PropertyBag_NoCache()
        {
            var propertyBag = new PropertyBag();

            TestEnum testEnum = propertyBag.GetProperty(TestEnumOptionTwo, cacheDefault: false);

            Assert.AreEqual(TestEnumOptionTwo.DefaultValue, testEnum);
            Assert.AreEqual(propertyBag.Count, 0);
        }

        [TestMethod]
        public void PropertyBag_TypedPropertyBagNoCache()
        {
            var propertyBag = new TestEnumPropertyBag();

            TestEnum testEnum = propertyBag.GetProperty(TestEnumOptionTwo, cacheDefault: false);

            Assert.AreEqual(TestEnumOptionTwo.DefaultValue, testEnum);
            Assert.AreEqual(propertyBag.Count, 0);
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

            Assert.IsFalse(propertyBag.TryGetProperty("testvalue", out converted));

            Assert.IsTrue(copiedPropertyBag.TryGetProperty("TestValue", out converted));
            Assert.AreEqual(expectedValue, converted, "String conversion to primitive type did not succeed.");

            Assert.IsTrue(copiedPropertyBag.TryGetProperty("testvalue", out converted));
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

            propertyBag = PersistAndReloadPropertyBag(propertyBag);
            testData.ValidatePropertyBag(propertyBag);

            string path = Path.GetTempFileName();

            try
            {
                propertyBag.SaveTo(path, "TestId");
                propertyBag = new PropertyBag();
                propertyBag.LoadFrom(path);
                testData.ValidatePropertyBag(propertyBag);
            }
            finally
            {
                if (File.Exists(path)) { File.Delete(path); }
            }

            TestEnumPropertyBag typedPropertyBag = propertyBag.GetProperty(TypedPropertyBagOption);

            typedPropertyBag.SetProperty(TestEnumOptionThree, TestEnum.ValueOne);
            Assert.AreEqual(TestEnum.ValueOne, typedPropertyBag.GetProperty(TestEnumOptionThree));
            Assert.AreNotEqual(TestEnumOptionThree.DefaultValue, typedPropertyBag.GetProperty(TestEnumOptionThree));

            Assert.AreEqual(TestEnumOptionTwo.DefaultValue, typedPropertyBag.GetProperty(TestEnumOptionTwo));

            propertyBag.SetProperty(TypedPropertyBagOption, null);
            typedPropertyBag = propertyBag.GetProperty(TypedPropertyBagOption);

            // The Roslyn options pattern has a design flaw in that it does not provide for handing out new 
            // default instances of references type. Instead, the option instance retains a singleton
            // value. In ModernCop, we replaced DefaultValue with a delegate that returns the appropriate type.
            // This allowed for multiple constructions of empty default values.
            //Assert.AreEqual(TestEnumOptionThree.DefaultValue, typedPropertyBag.GetProperty(TestEnumOptionThree));                
        }

        public PropertyBag PersistAndReloadPropertyBag(PropertyBag propertyBag)
        {
            PropertyBag result = null;
            string path = Path.GetTempFileName();
            try
            {
                using (var writer = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    propertyBag.SaveTo(writer, "TestProperties");
                }

                result = new PropertyBag();

                using (var reader = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    result.LoadFrom(reader);
                }
            }
            finally
            {
                try { File.Delete(path); } catch (IOException) { }
            }
            return result;
        }

        [TestMethod]
        public void PropertyBag_RemoveFromTypedPropertyBag()
        {
            var typedPropertyBag = new TypedPropertyBag<PropertyBag>();
            var propertyBag = typedPropertyBag.GetProperty(PropertyBagOption);

            Assert.AreEqual(1, typedPropertyBag.Count);

            typedPropertyBag.SetProperty(PropertyBagOption, null);
            Assert.AreEqual(0, typedPropertyBag.Count);
        }

        [ExpectedException(typeof(ArgumentNullException))]
        [TestMethod]
        public void PropertyBag_SaveToNull()
        {
            var propertyBag = new PropertyBag();
            propertyBag.SaveTo((string)null, "testId");
        }

        [ExpectedException(typeof(ArgumentNullException))]
        [TestMethod]
        public void PropertyBag_TypedPropertyBagGetNull()
        {
            var propertyBag = new TestEnumPropertyBag();
            propertyBag.GetProperty(null);
        }

        [ExpectedException(typeof(ArgumentNullException))]
        [TestMethod]
        public void PropertyBag_TypedPropertyBagSetNull()
        {
            var propertyBag = new TestEnumPropertyBag();
            propertyBag.SetProperty(null, 0);
        }

        [TestMethod]
        public void PropertyBag_SaveEmptyStream()
        {
            var propertyBag = new PropertyBag();
            propertyBag = PersistAndReloadPropertyBag(propertyBag);
            Assert.AreEqual(0, propertyBag.Count);

            string path = Path.GetTempFileName();
            try
            {
                propertyBag.SaveTo(path, "TestId");
                propertyBag = new PropertyBag();
                propertyBag.LoadFrom(path);
                Assert.AreEqual(0, propertyBag.Count);
            }
            finally
            {
                if (File.Exists(path)) { File.Delete(path); }
            }
        }

        [TestMethod]
        public void PropertyBag_SaveNullPropertyValue()
        {
            var propertyBag = new PropertyBag();
            propertyBag = PersistAndReloadPropertyBag(propertyBag);
        }

        [TestMethod]
        public void PropertyBag_SetNullKey()
        {
            var propertyBag = new PropertyBag();
            propertyBag["test"] = null;

            string stringResult;
            Assert.IsFalse(propertyBag.TryGetProperty<string>("test", out stringResult));
            Assert.IsNull(stringResult);
        }

        [TestMethod]
        [ExpectedException(typeof(NotSupportedException))]
        public void PropertyBag_UnsupportedTypeConversion()
        {
            var propertyBag = new PropertyBag();
            propertyBag["test"] = null;

            TestData testDataResult;
            propertyBag["test"] = "ThisStringCannotBeConvertedToAPropertyBag";
            Assert.IsFalse(propertyBag.TryGetProperty<TestData>("test", out testDataResult));
            Assert.IsNull(testDataResult);
        }

        struct MyStruct { }

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
                propertyBag.SetProperty(TestEnumOptionThree, TestEnumValue);
                propertyBag.SetProperty(StringSetOption, StringSetValue);

                PropertyBagValue = new PropertyBag();
                PropertyBagValue.SetProperty(DoubleOption, EmbeddedDoubleValue);
                PropertyBagValue.SetProperty(BooleanOption, EmbeddedBooleanValue);
                PropertyBagValue.SetProperty(TestEnumOptionThree, EmbeddedTestEnumValue);
                PropertyBagValue.SetProperty(StringSetOption, EmbeddedStringSetValue);


                propertyBag.SetProperty(PropertyBagOption, PropertyBagValue);

                TypedPropertyBagValue = propertyBag.GetProperty(TypedPropertyBagOption);
                TypedPropertyBagValue.SetProperty(TestEnumOptionThree, EmbeddedTestEnumValue);

                propertyBag.SetProperty(TypedPropertyBagOption, TypedPropertyBagValue);
            }

            internal void ValidatePropertyBag(PropertyBag propertyBag)
            {
                Assert.AreEqual(DoubleValue, propertyBag.GetProperty(DoubleOption));
                Assert.AreEqual(BooleanValue, propertyBag.GetProperty(BooleanOption));
                Assert.AreEqual(TestEnumValue, propertyBag.GetProperty(TestEnumOptionThree));

                ValidateStringSet(StringSetValue, propertyBag.GetProperty(StringSetOption));

                Assert.AreEqual(EmbeddedDoubleValue, propertyBag.GetProperty(PropertyBagOption).GetProperty(DoubleOption));
                Assert.AreEqual(EmbeddedBooleanValue, propertyBag.GetProperty(PropertyBagOption).GetProperty(BooleanOption));
                Assert.AreEqual(EmbeddedTestEnumValue, propertyBag.GetProperty(PropertyBagOption).GetProperty(TestEnumOptionThree));

                ValidateStringSet(EmbeddedStringSetValue, propertyBag.GetProperty(PropertyBagOption).GetProperty(StringSetOption));

                ValidatePropertyBag(PropertyBagValue, propertyBag.GetProperty(PropertyBagOption));
                ValidatePropertyBag(TypedPropertyBagValue, propertyBag.GetProperty(TypedPropertyBagOption));
            }
        }

        private const string TEST_FEATURE = "TestFeature";

        private static PerLanguageOption<bool> BooleanOption = 
            new PerLanguageOption<bool>(TEST_FEATURE, nameof(BooleanOption), false);

        private static PerLanguageOption<double> DoubleOption = 
            new PerLanguageOption<double>(TEST_FEATURE, nameof(DoubleOption), 4.7d);

        private static PerLanguageOption<PropertyBag> PropertyBagOption = 
            new PerLanguageOption<PropertyBag>(TEST_FEATURE, nameof(PropertyBagOption), new PropertyBag());

        private static PerLanguageOption<TestEnum> TestEnumOptionTwo =
            new PerLanguageOption<TestEnum>(TEST_FEATURE, nameof(TestEnumOptionTwo), TestEnum.ValueTwo);

        private static PerLanguageOption<TestEnum> TestEnumOptionThree =
            new PerLanguageOption<TestEnum>(TEST_FEATURE, nameof(TestEnumOptionThree), TestEnum.ValueThree);

        private static PerLanguageOption<StringSet> StringSetOption =
            new PerLanguageOption<StringSet>(TEST_FEATURE, nameof(StringSetOption), new StringSet(new string[] { "one", "two" }));

        private static PerLanguageOption<string> StringOption =
            new PerLanguageOption<string>(TEST_FEATURE, nameof(StringOption), null);

        [Serializable]
        enum TestEnum { ValueOne = 1, ValueTwo, ValueThree }

        private static PerLanguageOption<TestEnumPropertyBag> TypedPropertyBagOption =
            new PerLanguageOption<TestEnumPropertyBag>(TEST_FEATURE, nameof(TypedPropertyBagOption), new TestEnumPropertyBag());

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
