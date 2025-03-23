using ImGuiNET;
using System;
using System.IO; // dla File i Path
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config; // Tutaj dodajemy dla GamePaths
using VSImGui;
using VSImGui.API;
using System.Linq; // Dla OrderBy itp.
using System.Text;
using System.Numerics;
using Newtonsoft.Json;

namespace TemperatureMonitor
{
    public class TemperatureImGuiDialog
    {
        private readonly ICoreClientAPI api;
        private readonly Translation translation;
        private bool showWindow = false;
        private JObject temperatureData = new JObject();
        private float fontScale = 1.0f;
        private const float MIN_FONT_SCALE = 0.7f;
        private const float MAX_FONT_SCALE = 1.5f;
        private const float FONT_SCALE_STEP = 0.1f;
        private bool greenhouseMode = false;
        
        public TemperatureImGuiDialog(ICoreClientAPI api, Translation translation)
        {
            this.api = api;
            this.translation = translation;
            
            // Wczytaj ustawienia czcionki
            LoadUserSettings();
            
            // Rejestracja callbacka do rysowania GUI
            api.ModLoader.GetModSystem<ImGuiModSystem>().Draw += OnDraw;
        }
        
        public void ShowDialog(string jsonData)
        {
            try
            {
                if (!string.IsNullOrEmpty(jsonData))
                {
                    temperatureData = JObject.Parse(jsonData);
                }
                showWindow = true;
                api.ModLoader.GetModSystem<ImGuiModSystem>().Show();
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[TemperatureMonitor] Error parsing temperature data: {ex.Message}");
            }
        }
        
        private CallbackGUIStatus OnDraw(float deltaSeconds)
        {
            if (!showWindow) return CallbackGUIStatus.Closed;
            
            // Ustaw rozmiar okna przy pierwszym użyciu
            ImGui.SetNextWindowSize(new Vector2(500, 300), ImGuiCond.FirstUseEver);
            
            // Wycentruj okno przy pierwszym użyciu
            var displaySize = ImGui.GetIO().DisplaySize;
            var windowSize = new Vector2(500, 300);
            var windowPos = new Vector2(
                (displaySize.X - windowSize.X) * 0.5f,
                (displaySize.Y - windowSize.Y) * 0.5f
            );
            ImGui.SetNextWindowPos(windowPos, ImGuiCond.FirstUseEver);
            
            if (ImGui.Begin(translation.Get("temperature_history"), ref showWindow))
            {
                DrawTemperatureData();
            }
            ImGui.End();
            
            return showWindow ? CallbackGUIStatus.GrabMouse : CallbackGUIStatus.Closed;
        }
        
        private void DrawTemperatureData()
        {
            if (temperatureData.Count == 0)
            {
                ImGui.Text(translation.Get("no_data"));
                return;
            }
            
            // Powiększenie nagłówka
            float headerScale = 1.8f * fontScale;
            ImGui.SetWindowFontScale(headerScale);
            ImGui.Text(translation.Get("temperature_history_days"));
            ImGui.SetWindowFontScale(1.0f * fontScale);
            
            // Przyciski zmieniające rozmiar czcionki - umieszczone po lewej stronie pod nagłówkiem
            ImGui.SetCursorPos(new Vector2(10, ImGui.GetCursorPosY() + 5));

            // Dodaj etykietę "Font size"
            ImGui.Text(translation.Get("font_size") + ":");
            ImGui.SameLine();

            // Przycisk zmniejszania
            if (ImGui.Button("-", new Vector2(25, 25)))
            {
                fontScale = Math.Max(fontScale - FONT_SCALE_STEP, MIN_FONT_SCALE);
                SaveUserSettings();
            }

            ImGui.SameLine();
            // Wyświetl aktualny rozmiar z przesunięciem w górę
            float textY = ImGui.GetCursorPosY() - 2; // Przesuwamy tekst nieco wyżej
            ImGui.SetCursorPosY(textY);
            // Użyj szerszego pola dla tekstu, aby zmieścić znak %
            ImGui.SetNextItemWidth(40);
            ImGui.Text($"{(int)(fontScale * 100)}%%"); // Podwójny %% da jeden znak % w ImGui

            ImGui.SameLine();
            // Przycisk zwiększania
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2); // Przywracamy pozycję dla przycisku
            if (ImGui.Button("+", new Vector2(25, 25)))
            {
                fontScale = Math.Min(fontScale + FONT_SCALE_STEP, MAX_FONT_SCALE);
                SaveUserSettings();
            }

            // Po kontrolkach rozmiaru czcionki, dodajemy trochę odstępu i checkbox trybu szklarniowego
            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.SetCursorPosX(10);

            // Checkbox dla trybu szklarniowego
            bool currentGreenhouseMode = greenhouseMode;
            if (ImGui.Checkbox(translation.Get("greenhouse_mode"), ref currentGreenhouseMode))
            {
                greenhouseMode = currentGreenhouseMode;
                SaveUserSettings();
            }

            // Dodaj tooltip z informacją o trybie szklarniowym
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text(translation.Get("greenhouse_tooltip"));
                ImGui.EndTooltip();
            }

            // Powrót do normalnego przepływu UI
            ImGui.SetCursorPosX(0);
            ImGui.SetWindowFontScale(1.5f * fontScale);
            
            // Pasek separatora po nagłówku
            ImGui.Separator();
            ImGui.Spacing();
            
            // Nagłówki kolumn w jednej linii
            ImGui.Columns(3, "mainColumns", false);
            ImGui.SetColumnWidth(0, 380); // Kolumna z nazwami
            ImGui.SetColumnWidth(1, 150); // Kolumna Min
            ImGui.SetColumnWidth(2, 150); // Kolumna Max
            
            ImGui.Text(""); ImGui.NextColumn();
            ImGui.Text(translation.Get("min_temp")); ImGui.NextColumn();
            ImGui.Text(translation.Get("max_temp")); ImGui.NextColumn();
            ImGui.Separator();
            
            // Sortowanie lat malejąco
            var yearProps = temperatureData.Properties()
                .OrderByDescending(p => int.Parse(p.Name))
                .ToList();
            
            // Iteracja po latach
            foreach (var yearProp in yearProps)
            {
                string year = yearProp.Name;
                JObject? yearData = yearProp.Value as JObject;
                if (yearData == null) continue;
                
                // Pobranie min/max dla roku
                float yearMin = yearData["min"]?.Value<float>() ?? float.NaN;
                float yearMax = yearData["max"]?.Value<float>() ?? float.NaN;
                
                // Nagłówek roku
                bool yearOpen = false;
                ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.25f, 0.25f, 0.4f, 1.0f)); // Lekko niebieskawe tło dla roku
                
                ImGui.Columns(3, $"year_{year}", false); 
                ImGui.SetColumnWidth(0, 380);
                ImGui.SetColumnWidth(1, 150);
                ImGui.SetColumnWidth(2, 150);
                
                yearOpen = ImGui.TreeNodeEx($"{translation.Get("year")} {year}", ImGuiTreeNodeFlags.DefaultOpen);
                ImGui.NextColumn();
                ImGui.Text(string.Format("{0,10}",FormatTemperature(yearMin)));
                ImGui.NextColumn();
                ImGui.Text(string.Format("{0,10}",FormatTemperature(yearMax)));
                ImGui.NextColumn();
                
                ImGui.PopStyleColor();
                
                if (yearOpen)
                {
                    JObject? months = yearData["months"] as JObject;
                    if (months != null)
                    {
                        // Sortowanie miesięcy
                        var monthProps = months.Properties()
                            .OrderBy(p => int.Parse(p.Name))
                            .ToList();
                        
                        // Iteracja po miesiącach
                        foreach (var monthProp in monthProps)
                        {
                            string month = monthProp.Name;
                            JObject? monthData = monthProp.Value as JObject;
                            if (monthData == null) continue;
                            
                            // Pobranie min/max dla miesiąca
                            float monthMin = monthData["min"]?.Value<float>() ?? float.NaN;
                            float monthMax = monthData["max"]?.Value<float>() ?? float.NaN;
                            
                            // Nazwa miesiąca (skrót)
                            string monthName = translation.Get($"month_short_{int.Parse(month)}");
                            
                            // Subtelne tło dla miesiąca
                            ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.2f, 0.3f, 0.2f, 1.0f)); // Lekko zielonkawe tło
                            
                            ImGui.Columns(3, $"month_{year}_{month}", false);
                            ImGui.SetColumnWidth(0, 380);
                            ImGui.SetColumnWidth(1, 150);
                            ImGui.SetColumnWidth(2, 150);
                            
                            // Wcięcie dla miesięcy
                            ImGui.Indent(10);
                            bool monthOpen = ImGui.TreeNodeEx(monthName, ImGuiTreeNodeFlags.DefaultOpen);
                            ImGui.Unindent(10);
                            ImGui.NextColumn();
                            ImGui.Text(string.Format("{0,10}",FormatTemperature(monthMin)));
                            ImGui.NextColumn();
                            ImGui.Text(string.Format("{0,10}",FormatTemperature(monthMax)));
                            ImGui.NextColumn();
                            
                            ImGui.PopStyleColor();
                            
                            if (monthOpen)
                            {
                                JObject? days = monthData["days"] as JObject;
                                if (days != null)
                                {
                                    // Nagłówek dla dni
                                    ImGui.Columns(3, $"days_header_{year}_{month}", false);
                                    ImGui.SetColumnWidth(0, 380);
                                    ImGui.SetColumnWidth(1, 150);
                                    ImGui.SetColumnWidth(2, 150);
                                    
                                    ImGui.Indent(20);
                                    ImGui.Text(translation.Get("days"));
                                    ImGui.Unindent(20);
                                    ImGui.NextColumn();
                                    ImGui.Text(translation.Get("min_temp"));
                                    ImGui.NextColumn();
                                    ImGui.Text(translation.Get("max_temp"));
                                    ImGui.NextColumn();
                                    
                                    ImGui.Separator();
                                    
                                    // Sortowanie dni
                                    var dayProps = days.Properties()
                                        .OrderBy(p => int.Parse(p.Name))
                                        .ToList();
                                    
                                    // Alternatywne kolorowanie wierszy dla dni
                                    bool altRow = false;
                                    
                                    foreach (var dayProp in dayProps)
                                    {
                                        string day = dayProp.Name;
                                        JObject? dayData = dayProp.Value as JObject;
                                        if (dayData == null) continue;
                                        
                                        float dayMin = dayData["min"]?.Value<float>() ?? float.NaN;
                                        float dayMax = dayData["max"]?.Value<float>() ?? float.NaN;
                                        
                                        // Subtelne alternatywne kolorowanie wierszy
                                        if (altRow)
                                            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.9f, 0.9f, 0.9f, 1.0f));
                                        else
                                            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.8f, 0.8f, 0.8f, 1.0f));
                                        
                                        ImGui.Columns(3, $"day_{year}_{month}_{day}", false);
                                        ImGui.SetColumnWidth(0, 380);
                                        ImGui.SetColumnWidth(1, 150);
                                        ImGui.SetColumnWidth(2, 150);
                                        
                                        ImGui.Indent(20);
                                        ImGui.Text(day);
                                        ImGui.Unindent(20);
                                        ImGui.NextColumn();
                                        ImGui.Text(string.Format("{0,10}",FormatTemperature(dayMin)));
                                        ImGui.NextColumn();
                                        ImGui.Text(string.Format("{0,10}",FormatTemperature(dayMax)));
                                        ImGui.NextColumn();
                                        
                                        ImGui.PopStyleColor();
                                        altRow = !altRow;
                                    }
                                    
                                    ImGui.Spacing();
                                    ImGui.TreePop();
                                }
                            }
                        }
                    }
                    
                    ImGui.TreePop();
                }
                
                // Dodaj odstęp między latami
                ImGui.Spacing();
            }
            
            // Resetuj kolumny na końcu
            ImGui.Columns(1);
        }
        
        public void Dispose()
        {
            if (api != null)
            {
                api.ModLoader.GetModSystem<ImGuiModSystem>().Draw -= OnDraw;
            }
        }

        private void SaveUserSettings()
        {
            try
            {
                string configFolderPath = Path.Combine(GamePaths.DataPath, "ModData", "FontSettings");
                if (!Directory.Exists(configFolderPath))
                {
                    Directory.CreateDirectory(configFolderPath);
                }
                
                string configFilePath = Path.Combine(configFolderPath, "temperaturemonitor_settings.json");
                File.WriteAllText(configFilePath, JsonConvert.SerializeObject(new UserSettings 
                { 
                    FontScale = fontScale,
                    GreenhouseMode = greenhouseMode
                }));
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[TemperatureMonitor] Error saving user settings: {ex.Message}");
            }
        }

        private void LoadUserSettings()
        {
            try
            {
                string configFilePath = Path.Combine(GamePaths.DataPath, "ModData", "FontSettings", "temperaturemonitor_settings.json");
                if (File.Exists(configFilePath))
                {
                    string json = File.ReadAllText(configFilePath);
                    var settings = JsonConvert.DeserializeObject<UserSettings>(json);
                    if (settings != null)
                    {
                        if (settings.FontScale > 0)
                        {
                            fontScale = Math.Clamp(settings.FontScale, MIN_FONT_SCALE, MAX_FONT_SCALE);
                        }
                        greenhouseMode = settings.GreenhouseMode;
                    }
                }
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[TemperatureMonitor] Error loading user settings: {ex.Message}");
            }
        }

        // Dodaj klasę do przechowywania ustawień
        private class UserSettings
        {
            public float FontScale { get; set; } = 1.0f;
            public bool GreenhouseMode { get; set; } = false;
        }

// Zamiast bezpośrednio wyświetlać temperaturę, używamy funkcji pomocniczej
        private string FormatTemperature(float temperature)
        {
            if (float.IsNaN(temperature)) return "N/A";
            
            // Jeśli tryb szklarniowy jest włączony, dodaj 5°C do wyświetlanej wartości
            float displayTemp = greenhouseMode ? temperature + 5.0f : temperature;
            return $"{displayTemp:F1}°C";
        }

    }
}