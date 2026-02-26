using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using OB.Models;
using Prism.Mvvm;

namespace OB.ViewModels
{
    public class SettingsViewModel : BindableBase
    {
        private ObservableCollection<YoloClassSetting> _classSettings = new();
        public ObservableCollection<YoloClassSetting> ClassSettings
        {
            get => _classSettings;
            set => SetProperty(ref _classSettings, value);
        }

        public SettingsViewModel() { LoadSettings(); }

        public void LoadSettings()
        {
            if (!string.IsNullOrEmpty(OB.Default.mYoloLabels))
            {
                try
                {
                    var list = JsonSerializer.Deserialize<ObservableCollection<YoloClassSetting>>(OB.Default.mYoloLabels);
                    if (list != null) { ClassSettings = list; return; }
                }
                catch { }
            }
            ClassSettings = new ObservableCollection<YoloClassSetting>();
        }

        public void SaveSettings()
        {
            string json = JsonSerializer.Serialize(ClassSettings);
            OB.Default.mYoloLabels = json;
            OB.Default.Save();
        }

        public void SyncLabels(string[] labels)
        {
            bool changed = false;
            foreach (var label in labels)
            {
                if (!ClassSettings.Any(x => x.Label == label))
                {
                    ClassSettings.Add(new YoloClassSetting { Label = label });
                    changed = true;
                }
            }
            if (changed) SaveSettings();
        }
    }
}