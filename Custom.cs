using System.IO;
using System;

namespace Cliver.BotCustomization
{    
    
    // This class allows you to handle specific events on the settings class:
    //  The SettingChanging event is raised before a setting's value is changed.
    //  The PropertyChanged event is raised after a setting's value is changed.
    //  The SettingsLoaded event is raised after the setting values are loaded.
    //  The SettingsSaving event is raised before the setting values are saved.
    public sealed partial class Custom {

        public Custom() 
        {
            this.SettingsLoaded += _SettingsLoaded;
            this.SettingsSaving += _SettingsSaving;
        }

        void _SettingsSaving(object sender, System.ComponentModel.CancelEventArgs e)
        {
        }

        void _SettingsLoaded(object sender, System.Configuration.SettingsLoadedEventArgs e)
        {
            if (!UsersFile.Contains(":"))
            {
                if (!Directory.Exists(Cliver.Log.AppCommonDataDir))
                    Directory.CreateDirectory(Cliver.Log.AppCommonDataDir);
                string file2 = Cliver.Log.AppCommonDataDir + "\\" + Custom.Default.UsersFile;
                if (!File.Exists(file2))
                    File.Copy(UsersFile, file2);
                UsersFile = file2;
                Save();
            }
        }
    }
}
