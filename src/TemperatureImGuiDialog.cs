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
        
        public TemperatureImGuiDialog(ICoreClientAPI api, Translation translation)
        {
            this.api = api;
            this.translation = translation;
            
            // Wczytaj ustawienia czcionki
            LoadFontSettings();
            
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
                SaveFontSettings();
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
                SaveFontSettings();
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
                ImGui.Text(string.Format("{0,10}",$"{yearMin:F1}°C"));
                // ImGui.Text($"{yearMin:F1}°C");
                ImGui.NextColumn();
                ImGui.Text(string.Format("{0,10}",$"{yearMax:F1}°C"));
                // ImGui.Text($"{yearMax:F1}°C");
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
                            ImGui.Text(string.Format("{0,10}",$"{monthMin:F1}°C"));
                            // ImGui.Text($"{monthMin:F1}°C");
                            ImGui.NextColumn();
                            ImGui.Text(string.Format("{0,10}",$"{monthMax:F1}°C"));
                            // ImGui.Text($"{monthMax:F1}°C");
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
                                        ImGui.Text(string.Format("{0,10}",$"{dayMin:F1}°C"));
                                        // ImGui.Text($"{dayMin:F1}°C");
                                        ImGui.NextColumn();
                                        ImGui.Text(string.Format("{0,10}",$"{dayMax:F1}°C"));
                                        // ImGui.Text($"{dayMax:F1}°C");
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

        private void SaveFontSettings()
        {
            try
            {
                string configFolderPath = Path.Combine(GamePaths.DataPath, "ModData", "FontSettings");
                if (!Directory.Exists(configFolderPath))
                {
                    Directory.CreateDirectory(configFolderPath);
                }
                
                string configFilePath = Path.Combine(configFolderPath, "temperaturemonitor_font.json");
                File.WriteAllText(configFilePath, JsonConvert.SerializeObject(new FontSettings { FontScale = fontScale }));
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[TemperatureMonitor] Error saving font settings: {ex.Message}");
            }
        }

        private void LoadFontSettings()
        {
            try
            {
                string configFilePath = Path.Combine(GamePaths.DataPath, "ModData", "FontSettings", "temperaturemonitor_font.json");
                if (File.Exists(configFilePath))
                {
                    string json = File.ReadAllText(configFilePath);
                    // Użyjmy konkretnej klasy zamiast dynamic
                    var settings = JsonConvert.DeserializeObject<FontSettings>(json);
                    if (settings != null && settings.FontScale > 0)
                    {
                        fontScale = Math.Clamp(settings.FontScale, MIN_FONT_SCALE, MAX_FONT_SCALE);
                    }
                }
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[TemperatureMonitor] Error loading font settings: {ex.Message}");
            }
        }

        // Dodaj klasę do przechowywania ustawień
        private class FontSettings
        {
            public float FontScale { get; set; } = 1.0f;
        }

    }
}