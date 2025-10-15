using System;
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
            // Użyj wbudowanego systemu tłumaczeń BEZ langCode - użyje aktualnego języka gracza
            string translated = Lang.Get("temperaturemonitor:" + key);
            
            // Jeśli zwrócono klucz z prefiksem, oznacza to że tłumaczenie nie zostało znalezione
            string fullKey = "temperaturemonitor:" + key;
            if (translated == fullKey)
            {
                api.Logger.Warning($"[TemperatureMonitor] Translation NOT found for '{key}'");
                return key; // Zwróć sam klucz bez prefiksu
            }
            
            // Jeśli mamy argumenty do formatowania
            if (args != null && args.Length > 0)
            {
                try
                {
                    return string.Format(translated, args);
                }
                catch (FormatException ex)
                {
                    api.Logger.Error($"[TemperatureMonitor] Format error for key '{key}': {ex.Message}");
                    return translated; // Zwróć nieformatowany tekst
                }
            }
            
            return translated;
        }
    }
}