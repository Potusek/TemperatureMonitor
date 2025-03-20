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
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(500, 400), ImGuiCond.FirstUseEver);
            
            // Wycentruj okno przy pierwszym użyciu
            var displaySize = ImGui.GetIO().DisplaySize;
            var windowSize = new System.Numerics.Vector2(500, 400);
            var windowPos = new System.Numerics.Vector2(
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
            float headerScale = 1.5f;
            ImGui.SetWindowFontScale(headerScale);
            ImGui.Text(translation.Get("temperature_history_days"));
            
            float labelScale = 1.2f; // Mniejsza niż nagłówek główny (1.5f)
            ImGui.SetWindowFontScale(labelScale);
            ImGui.Columns(3);
            ImGui.Text(""); ImGui.NextColumn();
            ImGui.Text(translation.Get("min_temp")); ImGui.NextColumn();
            ImGui.Text(translation.Get("max_temp")); ImGui.NextColumn();

            ImGui.SetWindowFontScale(1.0f);
            
            ImGui.Separator();
            
            // Ustawienie większej czcionki dla danych
            float dataScale = 1.35f;
            
            // Sortowanie lat
            var yearProps = temperatureData.Properties().ToList();
            yearProps.Sort((a, b) => string.Compare(b.Name, a.Name)); // Sortowanie malejące
            
            // Iteracja po latach
            foreach (var yearProp in yearProps)
            {
                string year = yearProp.Name;
                JObject? yearData = yearProp.Value as JObject;
                
                if (yearData == null) continue;
                
                // Pobranie min/max dla roku
                JToken? minToken = yearData["min"];
                JToken? maxToken = yearData["max"];
                
                float yearMin = minToken != null ? minToken.Value<float>() : float.NaN;
                float yearMax = maxToken != null ? maxToken.Value<float>() : float.NaN;
                
                // Nagłówek roku z wartościami min/max w formie bardziej kompaktowej
                ImGui.SetWindowFontScale(dataScale);
                
                // Wyświetl rok i jego temperatury - w jednej linii
                ImGui.Columns(3);
                bool yearOpen = ImGui.TreeNode($"{translation.Get("year")} {year}###{year}");
                ImGui.NextColumn();
                ImGui.Text($"{yearMin:F1}°C");
                ImGui.NextColumn();
                ImGui.Text($"{yearMax:F1}°C");
                ImGui.NextColumn();
                ImGui.Columns(1);
                
                ImGui.SetWindowFontScale(1.0f);
                
                // Wyświetl miesiące, jeśli rok jest rozwinięty
                if (yearOpen)
                {
                    // Iteracja po miesiącach
                    JObject? months = yearData["months"] as JObject;
                    if (months != null)
                    {
                        // Sortowanie miesięcy
                        var monthProps = months.Properties().ToList();
                        monthProps.Sort((a, b) => int.Parse(b.Name).CompareTo(int.Parse(a.Name))); // Sortowanie malejące numeryczne
                        
                        // Nagłówki
                        ImGui.Indent(20.0f); // Wcięcie dla miesięcy
                        
                        foreach (var monthProp in monthProps)
                        {
                            string month = monthProp.Name;
                            JObject? monthData = monthProp.Value as JObject;
                            
                            if (monthData == null) continue;
                            
                            // Pobranie min/max dla miesiąca
                            minToken = monthData["min"];
                            maxToken = monthData["max"];
                            
                            float monthMin = minToken != null ? minToken.Value<float>() : float.NaN;
                            float monthMax = maxToken != null ? maxToken.Value<float>() : float.NaN;
                            
                            // Nazwa miesiąca
                            string monthName = translation.Get($"month_{int.Parse(month)}");
                            
                            // Wyświetl miesiąc i jego temperatury - w jednej linii
                            ImGui.SetWindowFontScale(dataScale);
                            ImGui.Columns(3);
                            bool monthOpen = ImGui.TreeNode($"{monthName}###{year}-{month}");
                            ImGui.NextColumn();
                            ImGui.Text($"{monthMin:F1}°C");
                            ImGui.NextColumn();
                            ImGui.Text($"{monthMax:F1}°C");
                            ImGui.NextColumn();
                            ImGui.Columns(1);
                            ImGui.SetWindowFontScale(1.0f);
                            
                            // Wyświetl dni, jeśli miesiąc jest rozwinięty
                            if (monthOpen)
                            {
                                // Iteracja po dniach
                                JObject? days = monthData["days"] as JObject;
                                if (days != null)
                                {
                                    // Nagłówki tabeli dla dni
                                    ImGui.Indent(20.0f); // Wcięcie dla dni
                                    ImGui.Columns(3, "days_table", true);
                                    ImGui.Text("Dzien");
                                    ImGui.NextColumn();
                                    ImGui.Text(translation.Get("min_temp"));
                                    ImGui.NextColumn();
                                    ImGui.Text(translation.Get("max_temp"));
                                    ImGui.NextColumn();
                                    ImGui.Separator();
                                    
                                    // Sortowanie dni
                                    var dayProps = days.Properties().ToList();
                                    dayProps.Sort((a, b) => int.Parse(b.Name).CompareTo(int.Parse(a.Name))); // Sortowanie malejące
                                    
                                    ImGui.SetWindowFontScale(dataScale);
                                    foreach (var dayProp in dayProps)
                                    {
                                        string day = dayProp.Name;
                                        JObject? dayData = dayProp.Value as JObject;
                                        
                                        if (dayData == null) continue;
                                        
                                        minToken = dayData["min"];
                                        maxToken = dayData["max"];
                                        
                                        float dayMin = minToken != null ? minToken.Value<float>() : float.NaN;
                                        float dayMax = maxToken != null ? maxToken.Value<float>() : float.NaN;
                                        
                                        ImGui.Text(day);
                                        ImGui.NextColumn();
                                        ImGui.Text($"{dayMin:F1}°C");
                                        ImGui.NextColumn();
                                        ImGui.Text($"{dayMax:F1}°C");
                                        ImGui.NextColumn();
                                    }
                                    ImGui.SetWindowFontScale(1.0f);
                                    
                                    ImGui.Columns(1);
                                    ImGui.Unindent(20.0f); // Cofnij wcięcie dla dni
                                }
                                
                                ImGui.TreePop();
                            }
                        }
                        
                        ImGui.Unindent(20.0f); // Cofnij wcięcie dla miesięcy
                    }
                    
                    ImGui.TreePop();
                }
            }
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