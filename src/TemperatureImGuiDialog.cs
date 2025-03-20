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

namespace TemperatureMonitor
{
    public class TemperatureImGuiDialog
    {
        private readonly ICoreClientAPI api;
        private readonly Translation translation;
        private bool showWindow = false;
        private JObject temperatureData = new JObject();
        
        public TemperatureImGuiDialog(ICoreClientAPI api, Translation translation)
        {
            this.api = api;
            this.translation = translation;
            
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
            float headerScale = 1.8f;
            ImGui.SetWindowFontScale(headerScale);
            ImGui.Text(translation.Get("temperature_history_days"));
            ImGui.SetWindowFontScale(1.5f);
            
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
                ImGui.Text(string.Format("{0,18}",$"{yearMin:F1}°C"));
                // ImGui.Text($"{yearMin:F1}°C");
                ImGui.NextColumn();
                ImGui.Text(string.Format("{0,18}",$"{yearMax:F1}°C"));
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
                            ImGui.Text(string.Format("{0,18}",$"{monthMin:F1}°C"));
                            // ImGui.Text($"{monthMin:F1}°C");
                            ImGui.NextColumn();
                            ImGui.Text(string.Format("{0,18}",$"{monthMax:F1}°C"));
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
                                        ImGui.Text(string.Format("{0,18}",$"{dayMin:F1}°C"));
                                        // ImGui.Text($"{dayMin:F1}°C");
                                        ImGui.NextColumn();
                                        ImGui.Text(string.Format("{0,18}",$"{dayMax:F1}°C"));
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
    }
}