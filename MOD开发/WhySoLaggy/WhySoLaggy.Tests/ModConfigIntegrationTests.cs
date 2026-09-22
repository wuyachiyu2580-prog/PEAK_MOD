using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WhySoLaggy.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class ModConfigIntegrationTests
    {
        private enum Mode { Persistent, Timed }
        private interface IConfigProperty { ConfigEntryBase ConfigBase { get; } }
        private sealed class WrappedSetting : IConfigProperty
        {
            private readonly ConfigEntryBase _entry;
            internal WrappedSetting(ConfigEntryBase entry) { _entry = entry; }
            ConfigEntryBase IConfigProperty.ConfigBase => _entry;
        }
        private sealed class Option { public string text { get; set; } }
        private abstract class InputSetting<T> { public string GetDisplayName() => typeof(T).Name; }
        private sealed class KeySetting : InputSetting<int> { }
        private sealed class PathSetting : InputSetting<string> { }
        private static class LanguageSource { public static Action Changed; }

        private static ConfigFile Config(string filename)
        {
            return new ConfigFile(Path.Combine(Path.GetTempPath(), "modconfig-regression", filename), false)
                { SaveOnConfigSet = false };
        }

        [TestMethod]
        public void Ownership_UsesConfigFileInsteadOfSharedSectionAndKey()
        {
            const string own = "com.wuyachiyu.WhySoLaggy.cfg";
            var a = Config(own).Bind("General", "Enabled", true);
            var b = Config("other.cfg").Bind("General", "Enabled", true);
            var suffixCollision = Config("prefix-" + own).Bind("General", "Enabled", true);
            Assert.IsTrue(ModConfigUiAdapter.Owns(a, own));
            Assert.IsFalse(ModConfigUiAdapter.Owns(b, own));
            Assert.IsFalse(ModConfigUiAdapter.Owns(suffixCollision, own));
            Assert.IsFalse(ModConfigUiAdapter.Owns(null, own));
            Assert.AreSame(a, ModConfigUiAdapter.Entry(new WrappedSetting(a)));
            Assert.IsNull(ModConfigUiAdapter.Entry(new object()));
            Assert.IsTrue(b.Value);
        }

        [TestMethod]
        public void Ownership_ReadsNonPublicInterfacePropertyUsedByModConfig182()
        {
            // ModConfig 1.8.2's IBepInExProperty.ConfigBase getter has Assembly visibility.
            var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(
                new AssemblyName("ModConfigContractFixture"), AssemblyBuilderAccess.Run);
            var module = assembly.DefineDynamicModule("Fixture");
            var contractBuilder = module.DefineType("IInternalConfig", TypeAttributes.Interface | TypeAttributes.Abstract | TypeAttributes.Public);
            var getter = contractBuilder.DefineMethod("get_ConfigBase", MethodAttributes.Assembly | MethodAttributes.Abstract |
                MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                typeof(ConfigEntryBase), Type.EmptyTypes);
            var property = contractBuilder.DefineProperty("ConfigBase", PropertyAttributes.None, typeof(ConfigEntryBase), Type.EmptyTypes);
            property.SetGetMethod(getter);
            var contract = contractBuilder.CreateType();
            Assert.IsNull(contract.GetProperty("ConfigBase"));
            var implementation = module.DefineType("WrappedConfig", TypeAttributes.Public);
            implementation.AddInterfaceImplementation(contract);
            var field = implementation.DefineField("Entry", typeof(ConfigEntryBase), FieldAttributes.Public);
            var method = implementation.DefineMethod("get_ConfigBase", MethodAttributes.Private | MethodAttributes.Virtual |
                MethodAttributes.Final | MethodAttributes.NewSlot | MethodAttributes.HideBySig, typeof(ConfigEntryBase), Type.EmptyTypes);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, field); il.Emit(OpCodes.Ret);
            implementation.DefineMethodOverride(method, contract.GetMethod("get_ConfigBase", BindingFlags.Instance | BindingFlags.NonPublic));
            var type = implementation.CreateType();
            object instance = Activator.CreateInstance(type);
            var entry = Config("nonpublic.cfg").Bind("General", "Enabled", true);
            type.GetField("Entry").SetValue(instance, entry);
            Assert.AreSame(entry, ModConfigUiAdapter.Entry(instance));
        }

        [TestMethod]
        public void EnumTranslation_PreservesRawChoicesIndicesAndStoredValuesAcrossLanguages()
        {
            var entry = Config("enum.cfg").Bind("Display", "Mode", Mode.Persistent);
            var raw = Enum.GetNames(typeof(Mode));
            var options = new List<Option> { new Option { text = raw[0] }, new Option { text = raw[1] } };
            var firstOption = options[0];
            for (int pass = 0; pass < 6; pass++)
            {
                bool chinese = pass % 2 == 0;
                ModConfigUiAdapter.ApplyOptionText(options, raw, key => chinese ? (key == "Persistent" ? "常驻" : "定时") : key);
                Assert.AreSame(firstOption, options[0]);
                CollectionAssert.AreEqual(new[] { "Persistent", "Timed" }, raw);
                Assert.AreEqual(chinese ? "定时" : "Timed", options[1].text);
                Assert.AreEqual(Mode.Persistent, entry.Value);
            }
            // The game's callback reads the unchanged raw choice by selected index.
            entry.Value = (Mode)Enum.Parse(typeof(Mode), raw[1]);
            Assert.AreEqual(Mode.Timed, entry.Value);
            entry.Value = (Mode)entry.DefaultValue;
            Assert.AreEqual(Mode.Persistent, entry.Value);
            ModConfigUiAdapter.ApplyOptionText(options, new[] { "wrong-count" }, _ => "unexpected");
            Assert.AreEqual("Timed", options[1].text);
        }

        [TestMethod]
        public void LanguageFieldSubscription_IsIdempotentAndPreservesOtherListeners()
        {
            int other = 0, own = 0;
            Action otherHandler = () => other++;
            Action ownHandler = () => own++;
            LanguageSource.Changed = otherHandler;
            var field = typeof(LanguageSource).GetField("Changed", BindingFlags.Static | BindingFlags.Public);
            var subscription = new ModConfigUiAdapter.ActionFieldSubscription();
            try
            {
                subscription.Subscribe(field, ownHandler);
                subscription.Subscribe(field, ownHandler);
                LanguageSource.Changed();
                Assert.AreEqual(1, own);
                Assert.AreEqual(1, other);
                subscription.Dispose();
                subscription.Dispose();
                LanguageSource.Changed();
                Assert.AreEqual(1, own);
                Assert.AreEqual(2, other);
                subscription.Subscribe(null, ownHandler);
                LanguageSource.Changed();
                Assert.AreEqual(1, own);
            }
            finally { subscription.Dispose(); LanguageSource.Changed = null; }
        }

        [TestMethod]
        public void HookResolution_ReturnsDeclaredClosedGenericMethodsAndSkipsMissingMembers()
        {
            MethodInfo key = ModConfigUiAdapter.DeclaredImplementation(typeof(KeySetting), "GetDisplayName", Type.EmptyTypes);
            MethodInfo path = ModConfigUiAdapter.DeclaredImplementation(typeof(PathSetting), "GetDisplayName", Type.EmptyTypes);
            Assert.AreEqual(typeof(InputSetting<int>), key.DeclaringType);
            Assert.AreEqual(key.DeclaringType, key.ReflectedType);
            Assert.AreEqual(typeof(InputSetting<string>), path.DeclaringType);
            Assert.AreNotEqual(key, path);
            Assert.IsFalse(key.ContainsGenericParameters);
            Assert.IsNull(ModConfigUiAdapter.DeclaredImplementation(typeof(InputSetting<>), "GetDisplayName", Type.EmptyTypes));
            Assert.IsNull(ModConfigUiAdapter.DeclaredImplementation(typeof(KeySetting), "RemovedMethod", Type.EmptyTypes));
            Assert.IsNull(ModConfigUiAdapter.DeclaredImplementation(null, "ShowSettings", Type.EmptyTypes));
        }
    }
}
