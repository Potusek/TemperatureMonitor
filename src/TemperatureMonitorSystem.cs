using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Config;
using Vintagestory.API.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common.Entities;

namespace TemperatureMonitor
{
    public class TemperatureMonitorSystem : ModSystem
    {
        // Stała definicja kodu skrótu klawiszowego
        private const string HOTKEY_CODE = "temperaturehistory";
        
        string? worldSpecificPath;
        private Translation? translation;
        // #pragma warning disable CS0169
        // private GuiDialogTemperature? temperatureDialog;
        // #pragma warning restore CS0169
        public ICoreAPI? api;
        public ICoreClientAPI? ClientApi;
        public ICoreServerAPI? ServerApi;
        private Dictionary<string, float> minTemperatures = new Dictionary<string, float>();
        private Dictionary<string, float> maxTemperatures = new Dictionary<string, float>();
        private double lastCheckGameHour = 0;

        private ModConfig? config;

        public override void Start(ICoreAPI api)
        {
            this.api = api;
            
            // Załaduj konfigurację
            config = ModConfig.Load(api);
            
            // Inicjalizacja tłumaczeń - domyślnie użyj "en"
            translation = new Translation(api, "en");
            
            string worldId = api?.World?.SavegameIdentifier.ToString() ?? string.Empty;
            this.worldSpecificPath = Path.Combine(GamePaths.DataPath, "ModData", worldId, "temperaturemonitor");

            this.api.Logger.Debug($"TemperatureMonitor: Start method called! Zapis będzie w {this.worldSpecificPath}");
            base.Start(api);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            this.ClientApi = api;
            
            // Pobierz język z ustawień klienta i zaktualizuj tłumaczenia
            translation = new Translation(this.ClientApi, Lang.CurrentLocale);
            
            try
            {
                // Zakomentuj lub usuń inicjalizację okna dialogowego
                // temperatureDialog = new GuiDialogTemperature(ClientApi, translation, HOTKEY_CODE);
                
                // Rejestracja klawisza skrótu
                api.Logger.Debug($"[TemperatureMonitor] Registering hotkey: {HOTKEY_CODE}");
                api.Input.RegisterHotKey(HOTKEY_CODE, translation.Get("temperature_history"), GlKeys.T, HotkeyType.GUIOrOtherControls, altPressed: true);
                api.Logger.Debug($"[TemperatureMonitor] Hotkey registration");
                
                // Prostszy handler dla klawisza skrótu
                api.Input.SetHotKeyHandler(HOTKEY_CODE, OnToggleTemperatureDialog);
            }
            catch (Exception ex)
            {
                ClientApi.Logger.Error($"[TemperatureMonitor] Error initializing: {ex.Message}\n{ex.StackTrace}");
            }
            
            api.Logger.Debug("TemperatureMonitor: Client-side start method called!");
            base.StartClientSide(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.ServerApi = api;
            
            // Wczytaj zapisane dane
            LoadSavedTemperatureData();
            
            // Resetuj licznik czasu
            lastCheckGameHour = 0;
            
            // Dodajemy log przed rejestracją komendy
            api.Logger.Debug("[TemperatureMonitor] Trying to register command 'tempsensor'");
            
            // Ignorujemy ostrzeżenie o przestarzałości i używamy podstawowej metody
            #pragma warning disable CS0618 // Wyłączamy ostrzeżenie o przestarzałości
            api.RegisterCommand("tempsensor", "Temperature sensor location commands", "[setspawn|setcurrlocation]", 
                (IServerPlayer player, int groupId, CmdArgs args) => {
                    api.Logger.Debug("[TemperatureMonitor] Command 'tempsensor' called by " + player.PlayerName);
                    HandleTempSensorCommand(player, groupId, args);
                }, "chat"); // Dodajemy podstawowe uprawnienie "chat"
            #pragma warning restore CS0618 // Włączamy z powrotem ostrzeżenie
            
            // Dodajemy log po rejestracji komendy
            api.Logger.Debug("[TemperatureMonitor] Command registered successfully");
            
            api.Event.RegisterGameTickListener(OnGameTick, 1000); // Sprawdzanie co sekundę
            api.Logger.Debug("TemperatureMonitor: Server-side start method called!");
            
            base.StartServerSide(api);
        }

        private void OnGameTick(float deltaTime)
        {
            if (this.ServerApi == null) return;
            
            // Sprawdź czas w grze zamiast rzeczywistego
            double gameHours = this.ServerApi.World.Calendar.TotalHours;
            
            // Zapisuj dane co pełną godzinę gry
            if (Math.Floor(gameHours) > Math.Floor(lastCheckGameHour))
            {
                this.ServerApi.Logger.Notification($"[TemperatureMonitor] Nowa godzina w grze: {Math.Floor(gameHours)}");
                lastCheckGameHour = gameHours;
                CheckTemperatures();
            }
        }
 
        private void CheckTemperatures()
        {
            if (this.ServerApi == null || config == null) return;
            
            // Pobierz datę z czasu gry
            int year = this.ServerApi.World.Calendar.Year;
            int month = this.ServerApi.World.Calendar.Month; // 1-12
            int dayOfMonth = (int)(this.ServerApi.World.Calendar.DayOfYear % this.ServerApi.World.Calendar.DaysPerMonth) + 1;
            
            // Format: Rok-Miesiąc-Dzień (np. 2-07-03 dla 3 lipca roku 2)
            string gameDate = $"{year}-{month:D2}-{dayOfMonth:D2}";
            
            try
            {
                BlockPos measurementPosition;
                
                // Wybierz miejsce pomiaru na podstawie konfiguracji
                if (config.UseSpawnPoint || !config.MeasurementX.HasValue || !config.MeasurementY.HasValue || !config.MeasurementZ.HasValue)
                {
                    // Pobierz punkt spawnu
                    EntityPos spawnEntityPos = this.ServerApi.World.DefaultSpawnPosition;
                    measurementPosition = new BlockPos((int)spawnEntityPos.X, (int)spawnEntityPos.Y, (int)spawnEntityPos.Z);
                    ServerApi.Logger.Debug($"[TemperatureMonitor] Measuring at spawn point: {measurementPosition.X}, {measurementPosition.Y}, {measurementPosition.Z}");
                }
                else
                {
                    // Użyj skonfigurowanych koordynatów
                    measurementPosition = new BlockPos(
                        config.MeasurementX.Value,
                        config.MeasurementY.Value,
                        config.MeasurementZ.Value
                    );
                    ServerApi.Logger.Debug($"[TemperatureMonitor] Measuring at configured location: {measurementPosition.X}, {measurementPosition.Y}, {measurementPosition.Z}");
                }

                // Pobierz temperaturę w wybranym punkcie
                float currentTemp = this.ServerApi.World.BlockAccessor.GetClimateAt(measurementPosition).Temperature;
                
                // Aktualizuj min temperaturę
                if (!minTemperatures.ContainsKey(gameDate) || currentTemp < minTemperatures[gameDate])
                {
                    minTemperatures[gameDate] = currentTemp;
                }
                
                // Aktualizuj max temperaturę
                if (!maxTemperatures.ContainsKey(gameDate) || currentTemp > maxTemperatures[gameDate])
                {
                    maxTemperatures[gameDate] = currentTemp;
                }
                
                // Zapisz do logu
                LogTemperature(currentTemp, gameDate);
            }
            catch (Exception ex)
            {
                this.ServerApi.Logger.Error("Temperature Logger error: " + ex.Message);
            }
            
            // Zapisz wyniki do pliku
            SaveTemperatureData();
        }

        private void LogTemperature(float temperature, string gameDate)
        {
            if (this.ServerApi == null) return;
            
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); // Możemy zachować rzeczywisty czas dla celów logowania
            this.ServerApi.Logger.Notification($"[TemperatureMonitor] {timestamp} - Game date: {gameDate}, Temperature: {temperature:F1}°C");
        }
        
        private void SaveTemperatureData()
        {
            if (this.ServerApi == null) 
            {
                // Dodaj log
                if (api != null) api.Logger.Error("[TemperatureMonitor] SaveTemperatureData: ServerApi jest null!");
                return;
            }
            
            try
            {
                // Dodaj log o liczbie danych
                this.ServerApi.Logger.Notification($"[TemperatureMonitor] Próba zapisania danych, liczba wpisów: {minTemperatures.Count}");
                
                // Nie zapisuj jeśli nie ma danych
                if (minTemperatures.Count == 0)
                {
                    this.ServerApi.Logger.Notification("[TemperatureMonitor] Brak danych do zapisania.");
                    return;
                }
                
                // Przygotuj obiekt JSON
                var temperatureData = new JObject();
                
                foreach (var entry in minTemperatures)
                {
                    string date = entry.Key;
                    
                    float minTemp = entry.Value;
                    float maxTemp = maxTemperatures[date];
                    
                    // Jeśli już mamy wpis dla tej daty, wybierz bardziej ekstremalne wartości
                    if (temperatureData.ContainsKey(date))
                    {
                        JObject? existingData = temperatureData[date] as JObject;
                        if (existingData != null)
                        {
                            // Bezpieczne pobieranie wartości z sprawdzeniem null
                            float existingMin = existingData["min"]?.Value<float>() ?? float.MaxValue;
                            float existingMax = existingData["max"]?.Value<float>() ?? float.MinValue;
                            
                            minTemp = Math.Min(minTemp, existingMin);
                            maxTemp = Math.Max(maxTemp, existingMax);
                        }
                    }
                    
                    temperatureData[date] = new JObject
                    {
                        ["min"] = minTemp,
                        ["max"] = maxTemp
                    };
                }
                
                // Dodaj dodatkowy log dla ścieżki
                this.ServerApi.Logger.Notification($"[TemperatureMonitor] Ścieżka do folderu: {this.worldSpecificPath}");
                
                // Upewnij się, że folder istnieje
                if (this.worldSpecificPath != null)
                {
                    if (!Directory.Exists(worldSpecificPath))
                    {
                        this.ServerApi.Logger.Notification($"[TemperatureMonitor] Tworzenie katalogu: {worldSpecificPath}");
                        Directory.CreateDirectory(worldSpecificPath);
                    }
                    
                    string filePath = Path.Combine(worldSpecificPath, "TemperatureMonitorlog.json");
                    this.ServerApi.Logger.Notification($"[TemperatureMonitor] Zapisywanie do pliku: {filePath}");
                    
                    // Zapisz dane
                    File.WriteAllText(filePath, temperatureData.ToString(Formatting.Indented));
                    
                    // Sprawdź czy plik istnieje po zapisie
                    if (File.Exists(filePath))
                    {
                        this.ServerApi.Logger.Notification($"[TemperatureMonitor] Plik został pomyślnie zapisany.");
                    }
                    else
                    {
                        this.ServerApi.Logger.Error($"[TemperatureMonitor] BŁĄD: Plik nie istnieje po próbie zapisu!");
                    }
                }
                else
                {
                    this.ServerApi.Logger.Error("[TemperatureMonitor] BŁĄD: worldSpecificPath jest null!");
                }
            }
            catch (Exception ex)
            {
                this.ServerApi.Logger.Error($"[TemperatureMonitor] Błąd zapisu danych: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }

        private bool OnToggleTemperatureDialog(KeyCombination comb)
        {
            try
            {
                if (ClientApi == null || translation == null) return false;
                
                ClientApi.Logger.Debug("[TemperatureMonitor] Alt+T pressed, showing temperature data");
                
                // Wyświetl dane w oknie czatu
                ShowTemperatureData();
                
                return true;
            }
            catch (Exception ex)
            {
                ClientApi?.Logger.Error($"[TemperatureMonitor] Error: {ex.Message}\n{ex.StackTrace}");
                if (ClientApi != null)
                    ClientApi.ShowChatMessage($"Error: {ex.Message}");
            }
            
            return false;
        }

        private void LoadSavedTemperatureData()
        {
            if (ServerApi == null || worldSpecificPath == null) return;
            
            string filePath = Path.Combine(worldSpecificPath, "TemperatureMonitorlog.json");
            
            if (File.Exists(filePath))
            {
                try
                {
                    string jsonContent = File.ReadAllText(filePath);
                    if (string.IsNullOrWhiteSpace(jsonContent) || jsonContent == "{}")
                    {
                        ServerApi.Logger.Notification("[TemperatureMonitor] Plik danych jest pusty, brak poprzednich zapisów.");
                        return;
                    }
                    
                    JObject temperatureData = JObject.Parse(jsonContent);
                    
                    foreach (var prop in temperatureData.Properties())
                    {
                        string date = prop.Name;
                        JObject? tempObj = prop.Value as JObject;
                        
                        if (tempObj != null && tempObj["min"] != null && tempObj["max"] != null)
                        {
                            if (tempObj.TryGetValue("min", out JToken? minToken) && 
                                tempObj.TryGetValue("max", out JToken? maxToken))
                            {
                                if (minToken != null && maxToken != null && 
                                    minToken.Type != JTokenType.Null && maxToken.Type != JTokenType.Null)
                                {
                                    // Używaj ToObject zamiast Value
                                    float min = (float)minToken.ToObject<double>();
                                    float max = (float)maxToken.ToObject<double>();
                                    
                                    minTemperatures[date] = min;
                                    maxTemperatures[date] = max;
                                }
                            }
                        }
                    }
                    
                    ServerApi.Logger.Notification($"[TemperatureMonitor] Wczytano {minTemperatures.Count} zapisów temperatur z pliku.");
                }
                catch (Exception ex)
                {
                    ServerApi.Logger.Error($"[TemperatureMonitor] Błąd wczytywania zapisanych danych: {ex.Message}");
                }
            }
        }

        public override void Dispose()
        {
            // Upewnij się, że wszystkie dane są zapisane przy wyładowaniu moda
            if (ServerApi != null && minTemperatures.Count > 0)
            {
                ServerApi.Logger.Notification($"[TemperatureMonitor] Zamykanie - zapisywanie {minTemperatures.Count} temperatur");
                SaveTemperatureData();
            }
            
            // Usuń lub zakomentuj tę linię
            // temperatureDialog?.Dispose();
            
            base.Dispose();
        }

        private void ShowTemperatureData()
        {
            if (ClientApi == null || translation == null) return;
            
            // ClientApi.ShowChatMessage(translation.Get("hotkey_detected"));
            
            string worldId = ClientApi.World.SavegameIdentifier.ToString();
            string jsonPath = Path.Combine(GamePaths.DataPath, "ModData", worldId, "temperaturemonitor", "TemperatureMonitorlog.json");
            
            // ClientApi.ShowChatMessage($"Looking for data at: {jsonPath}");
            
            if (File.Exists(jsonPath))
            {
                try {
                    // ClientApi.ShowChatMessage("Data file found!");
                    
                    string jsonContent = File.ReadAllText(jsonPath);
                    JObject temperatureData = JObject.Parse(jsonContent);
                    
                    // Wyświetl dane w bardziej czytelny sposób
                    ClientApi.ShowChatMessage(translation.Get("temperature_history") + ":");
                    
                    int recordCount = temperatureData.Count;
                    // ClientApi.ShowChatMessage($"Found {recordCount} temperature records");
                    
                    foreach (var prop in temperatureData.Properties())
                    {
                        string date = prop.Name;
                        JObject? tempObj = prop.Value as JObject;
                        
                        if (tempObj != null)
                        {
                            try
                            {
                                // Bezpieczniejsza metoda pobierania wartości
                                if (tempObj.TryGetValue("min", out JToken? minToken) && 
                                    tempObj.TryGetValue("max", out JToken? maxToken))
                                {
                                    if (minToken != null && maxToken != null)
                                    {
                                        float min = (float)minToken.ToObject<double>();
                                        float max = (float)maxToken.ToObject<double>();
                                        
                                        ClientApi.ShowChatMessage($"{date}: {translation.Get("min_temp")}: {min:F1}°C, {translation.Get("max_temp")}: {max:F1}°C");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                ClientApi.ShowChatMessage(translation.Get("temperature_data_processing_error", date, ex.Message));
                            }
                        }
                    }
                }
                catch (Exception ex) {
                    ClientApi.ShowChatMessage(translation.Get("reading_data_error", ex.Message));
                }
            }
            else
            {
                ClientApi.ShowChatMessage(translation.Get("no_data"));
            }
        }

        private void HandleTempSensorCommand(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (translation == null) return;
            
            if (args.Length == 0)
            {
                // Wyświetl aktualną lokalizację sensora
                ShowSensorLocation(player, groupId);
                return;
            }

            string subCommand = args[0].ToLowerInvariant();
            
            switch (subCommand)
            {
                case "setspawn":
                    // Ustaw sensor na punkt spawnu
                    if (config != null && ServerApi != null)
                    {
                        config.UseSpawnPoint = true;
                        config.MeasurementX = null;
                        config.MeasurementY = null;
                        config.MeasurementZ = null;
                        config.Save(ServerApi);
                        
                        // Pobierz aktualny punkt spawnu do wyświetlenia
                        EntityPos spawnPos = ServerApi.World.DefaultSpawnPosition;
                        player.SendMessage(groupId, translation.Get("sensor_set_spawn", (int)spawnPos.X, (int)spawnPos.Y, (int)spawnPos.Z), EnumChatType.Notification);
                    }
                    break;
                    
                case "setcurrlocation":
                    // Ustaw sensor na aktualną pozycję gracza
                    if (config != null && ServerApi != null)
                    {
                        EntityPos playerPos = player.Entity.Pos;
                        config.UseSpawnPoint = false;
                        config.MeasurementX = (int)playerPos.X;
                        config.MeasurementY = (int)playerPos.Y;
                        config.MeasurementZ = (int)playerPos.Z;
                        config.Save(ServerApi);
                        
                        player.SendMessage(groupId, translation.Get("sensor_set_location", config.MeasurementX, config.MeasurementY, config.MeasurementZ), EnumChatType.Notification);
                    }
                    break;
                    
                default:
                    player.SendMessage(groupId, translation.Get("command_unknown"), EnumChatType.Notification);
                    break;
            }
        }

        private void ShowSensorLocation(IServerPlayer player, int groupId)
        {
            if (player == null || config == null || ServerApi == null || translation == null) 
                return;
            
            if (config.UseSpawnPoint)
            {
                EntityPos spawnPos = ServerApi.World.DefaultSpawnPosition;
                player.SendMessage(groupId, translation.Get("sensor_current_spawn", (int)spawnPos.X, (int)spawnPos.Y, (int)spawnPos.Z), EnumChatType.Notification);
            }
            else if (config.MeasurementX.HasValue && config.MeasurementY.HasValue && config.MeasurementZ.HasValue)
            {
                player.SendMessage(groupId, translation.Get("sensor_current_location", config.MeasurementX, config.MeasurementY, config.MeasurementZ), EnumChatType.Notification);
            }
            else
            {
                player.SendMessage(groupId, translation.Get("sensor_not_configured"), EnumChatType.Notification);
            }
        }

     }
}