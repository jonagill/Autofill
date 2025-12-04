using System;
using UnityEditor;
using UnityEditor.SettingsManagement;

namespace Autofill
{
    public static class AutofillEditorSettings
    {
        /// <summary>
        /// Custom UserSetting type that routes to our settings instance
        /// </summary>
        private class Setting<T> : UserSetting<T>
        {
            public Setting(string key, T value, SettingsScope scope = SettingsScope.User)
                : base(Instance, key, value, scope)
            { }

            public Setting(Settings settings, string key, T value, SettingsScope scope = SettingsScope.User)
                : base(settings, key, value, scope) { }
        }
        
        /// <summary>
        /// Registers our settings for display in the Project Settings GUI
        /// </summary>
        static class AutofillUserSettingsProvider
        {
            private const string SettingsPath = "Preferences/Autofill";
            
            [SettingsProvider]
            static SettingsProvider CreateSettingsProvider()
            {
                var provider = new UserSettingsProvider(SettingsPath,
                    Instance,
                    new [] { typeof(AutofillAttribute).Assembly, typeof(AutofillEditorSettings).Assembly },
                    SettingsScope.User);
                
                return provider;
            }
        }
        
        private const string PackageName = "com.jonagill.autofill";
        private const string CategoryName = "Autofill";
        
        private static Settings _instance;
        private static Settings Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new Settings(PackageName);

                return _instance;
            }
        }

        
        [UserSetting(
            category: CategoryName, 
            title: "Display all Autofill fields in Inspector",
            tooltip: "If true, Autofilled fields will always be displayed in the Inspector window, even if AlwaysShowInInspector is false.")]
        private static Setting<bool> _displayAllFieldsInInspector = new ($"{PackageName}.DisplayAllFieldsInInspector", false);
        
        /// <summary>
        /// If true, Autofilled fields will always be displayed in the Inspector window, even if AlwaysShowInInspector is false
        /// </summary>
        public static bool DisplayAllFieldsInInspector
        {
            get => _displayAllFieldsInInspector.value;
            set => _displayAllFieldsInInspector.SetValue(value);
        }

        public static void RegisterSettingsChangedCallback(Action callback)
        {
            Instance.afterSettingsSaved += callback;
        }
        
        public static void UnregisterSettingsChangedCallback(Action callback)
        {
            Instance.afterSettingsSaved -= callback;
        }
    }
}
