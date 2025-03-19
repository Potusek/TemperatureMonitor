using Newtonsoft.Json;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace TemperatureMonitor
{
    public class ModConfig
    {
        public string HotkeyCode { get; set; } = "alt-t";
        
        // Konfiguracja sensora temperatury
        public bool UseSpawnPoint { get; set; } = true;
        public int? MeasurementX { get; set; } = null;
        public int? MeasurementY { get; set; } = null;
        public int? MeasurementZ { get; set; } = null;
        
        public void Save(ICoreAPI api)
        {
            string configFile = "temperaturemonitorconfig.json";
            api.StoreModConfig(this, configFile);
        }
        
        public static ModConfig Load(ICoreAPI api)
        {
            string configFile = "temperaturemonitorconfig.json";
            try
            {
                ModConfig? config = api.LoadModConfig<ModConfig>(configFile);
                if (config == null)
                {
                    config = new ModConfig();
                    api.StoreModConfig(config, configFile);
                }
                return config;
            }
            catch (Exception ex)
            {
                api.Logger.Error("[TemperatureMonitor] Error loading config: " + ex.Message);
                return new ModConfig();
            }
        }
    }
}