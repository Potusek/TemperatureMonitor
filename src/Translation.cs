using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace TemperatureMonitor
{
    public class Translation
    {
        private ICoreAPI api;
        private string langCode;

        public Translation(ICoreAPI api, string langCode)
        {
            this.api = api;
            this.langCode = langCode;
        }

        public string Get(string key, params object[]? args)
        {
            // Użyj wbudowanego systemu tłumaczeń
            string translated = Lang.Get("temperaturemonitor:" + key, langCode);
            
            // Jeśli zwrócono ten sam klucz, oznacza to, że tłumaczenie nie zostało znalezione
            if (translated == "temperaturemonitor:" + key)
            {
                api.Logger.Debug($"[TemperatureMonitor] Translation NOT found for '{key}'");
                return key;
            }
            
            api.Logger.Debug($"[TemperatureMonitor] Translation found for '{key}': '{translated}'");
            
            // Jeśli mamy argumenty do formatowania
            if (args != null && args.Length > 0)
            {
                return string.Format(translated, args);
            }
            
            return translated;
        }
    }
}