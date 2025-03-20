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
using ProtoBuf;
using System.Linq;

namespace TemperatureMonitor
{
    // Klasy do przesyłania wiadomości sieciowych
    [ProtoContract]
    public class TemperatureDataRequest
    {
        // Pusta klasa, tylko sama prośba o dane
    }

    [ProtoContract]
    public class TemperatureDataResponse
    {
        [ProtoMember(1)]
        public bool Success { get; set; }
        
        [ProtoMember(2)]
        public string JsonData { get; set; } = "";
    }

    public class TemperatureMonitorSystem : ModSystem
    {
        // Stała definicja kodu skrótu klawiszowego
        private const string HOTKEY_CODE = "temperaturehistory";
        
        string? worldSpecificPath;
        private Translation? translation;
        private TemperatureImGuiDialog? temperatureImGuiDialog;
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
            try
            {
                this.ClientApi = api;
                
                // Pobierz język z ustawień klienta i zaktualizuj tłumaczenia
                translation = new Translation(this.ClientApi, Lang.CurrentLocale);
                
                // Inicjalizacja interfejsu ImGui
                temperatureImGuiDialog = new TemperatureImGuiDialog(api, translation);
                
                // Rejestracja kanału i wiadomości sieciowych
                api.Logger.Debug("[TemperatureMonitor] Registering network channel");
                var channel = api.Network.RegisterChannel("temperaturemonitor");
                api.Logger.Debug("[TemperatureMonitor] Registering message types");
                channel.RegisterMessageType<TemperatureDataRequest>();
                channel.RegisterMessageType<TemperatureDataResponse>();
                api.Logger.Debug("[TemperatureMonitor] Setting message handler");
                channel.SetMessageHandler<TemperatureDataResponse>(OnTemperatureDataResponse);
                
                // Rejestracja klawisza skrótu
                api.Logger.Debug("[TemperatureMonitor] Registering hotkey");
                api.Input.RegisterHotKey(HOTKEY_CODE, translation.Get("temperature_history"), GlKeys.T, HotkeyType.GUIOrOtherControls, altPressed: true);
                api.Input.SetHotKeyHandler(HOTKEY_CODE, OnToggleTemperatureDialog);
                
                api.Logger.Debug("[TemperatureMonitor] Client-side start method called!");
            }
            catch (Exception ex)
            {
                api.Logger.Error($"[TemperatureMonitor] Error in StartClientSide: {ex.Message}\n{ex.StackTrace}");
            }
            
            base.StartClientSide(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.ServerApi = api;
            
            // Wczytaj zapisane dane
            LoadSavedTemperatureData();
            
            // Resetuj licznik czasu
            lastCheckGameHour = 0;
            
            // Rejestruj kanał i wiadomości sieciowe
            api.Network.RegisterChannel("temperaturemonitor")
               .RegisterMessageType<TemperatureDataRequest>()
               .RegisterMessageType<TemperatureDataResponse>()
               .SetMessageHandler<TemperatureDataRequest>(OnTemperatureDataRequest);
            
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

        // Handler żądania danych temperatury od klienta
        private void OnTemperatureDataRequest(IServerPlayer fromPlayer, TemperatureDataRequest message)
        {
            if (ServerApi == null || worldSpecificPath == null) return;
            
            ServerApi.Logger.Debug($"[TemperatureMonitor] Received temperature data request from player {fromPlayer.PlayerName}");
            
            string filePath = Path.Combine(worldSpecificPath, "TemperatureMonitorlog.json");
            if (!File.Exists(filePath)) 
            {
                ServerApi.Logger.Debug($"[TemperatureMonitor] Temperature data file not found at: {filePath}");
                // Wysyłamy pustą odpowiedź
                ServerApi.Network.GetChannel("temperaturemonitor")
                    .SendPacket(new TemperatureDataResponse { Success = false }, fromPlayer);
                return;
            }
            
            try
            {
                string jsonContent = File.ReadAllText(filePath);
                ServerApi.Logger.Debug($"[TemperatureMonitor] Sending temperature data to player {fromPlayer.PlayerName}, data size: {jsonContent.Length} bytes");
                ServerApi.Network.GetChannel("temperaturemonitor")
                    .SendPacket(new TemperatureDataResponse 
                    { 
                        Success = true, 
                        JsonData = jsonContent 
                    }, fromPlayer);
            }
            catch (Exception ex)
            {
                ServerApi.Logger.Error($"[TemperatureMonitor] Error reading temperature data: {ex.Message}");
                ServerApi.Network.GetChannel("temperaturemonitor")
                    .SendPacket(new TemperatureDataResponse { Success = false }, fromPlayer);
            }
        }

        // Handler odpowiedzi z danymi temperatury od serwera
        private void OnTemperatureDataResponse(TemperatureDataResponse message)
        {
            if (ClientApi == null || translation == null) return;
            
            ClientApi.Logger.Debug($"[TemperatureMonitor] Received temperature data response, success: {message.Success}");
            
            if (!message.Success)
            {
                ClientApi.ShowChatMessage(translation.Get("no_data"));
                return;
            }
            
            // Zamiast wyświetlać dane w chacie, użyj interfejsu ImGui
            temperatureImGuiDialog?.ShowDialog(message.JsonData);
        }

        // Nowa metoda do wyświetlania danych z otrzymanego JSON-a
        private void DisplayTemperatureData(string jsonData)
        {
            if (ClientApi == null || translation == null) return;
            
            try
            {
                ClientApi.Logger.Debug($"[TemperatureMonitor] Parsing temperature data, size: {jsonData.Length} bytes");
                JObject temperatureData = JObject.Parse(jsonData);
                
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
            catch (Exception ex)
            {
                ClientApi.Logger.Error($"[TemperatureMonitor] Error parsing temperature data: {ex.Message}");
                ClientApi.ShowChatMessage(translation.Get("reading_data_error", ex.Message));
            }
        }

        private void OnGameTick(float deltaTime)
        {
            if (this.ServerApi == null) return;
            
            // Sprawdź czas w grze zamiast rzeczywistego
            double gameHours = this.ServerApi.World.Calendar.TotalHours;
            
            // Co 15 minut gry (4 razy na godzinę)
            if (Math.Floor(gameHours * 4) > Math.Floor(lastCheckGameHour * 4))
            {
                this.ServerApi.Logger.Notification($"[TemperatureMonitor] Checking temperature at game hour: {gameHours:F1}");
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
                if (api != null) api.Logger.Error("[TemperatureMonitor] SaveTemperatureData: ServerApi jest null!");
                return;
            }
            
            try
            {
                this.ServerApi.Logger.Notification($"[TemperatureMonitor] Próba zapisania danych, liczba wpisów: {minTemperatures.Count}");
                // Dodaj dokładniejsze informacje o zapisywanych danych
                this.ServerApi.Logger.Debug($"[TemperatureMonitor] DEBUG: Zapisywane dni: {string.Join(", ", minTemperatures.Keys)}");
                
                // Nie zapisuj jeśli nie ma danych
                if (minTemperatures.Count == 0)
                {
                    this.ServerApi.Logger.Notification("[TemperatureMonitor] Brak danych do zapisania.");
                    return;
                }
                
                // Przygotuj hierarchiczną strukturę JSON
                var temperatureData = new JObject();
                
                foreach (var entry in minTemperatures)
                {
                    string date = entry.Key;
                    // Format daty: Rok-Miesiąc-Dzień (np. 2-07-03)
                    string[] dateParts = date.Split('-');
                    if (dateParts.Length != 3) 
                    {
                        this.ServerApi.Logger.Error($"[TemperatureMonitor] Nieprawidłowy format daty: {date}");
                        continue;
                    }
                    
                    string year = dateParts[0];
                    string month = dateParts[1];
                    string day = dateParts[2].TrimStart('0'); // Usuwamy wiodące zera
                    
                    // Pobierz wartości temperatury
                    float minTemp = entry.Value;
                    float maxTemp = maxTemperatures.ContainsKey(date) ? maxTemperatures[date] : float.MinValue;
                    
                    // Upewnij się, że mamy obiekt roku
                    if (!temperatureData.ContainsKey(year))
                    {
                        temperatureData[year] = new JObject
                        {
                            ["min"] = float.MaxValue,
                            ["max"] = float.MinValue,
                            ["months"] = new JObject()
                        };
                    }
                    
                    JObject? yearData = temperatureData[year] as JObject;
                    if (yearData == null) 
                    {
                        this.ServerApi.Logger.Error($"[TemperatureMonitor] Nie można utworzyć obiektu roku: {year}");
                        continue;
                    }
                    
                    JObject? months = yearData["months"] as JObject;
                    if (months == null) 
                    {
                        this.ServerApi.Logger.Error($"[TemperatureMonitor] Nie można utworzyć obiektu miesięcy dla roku: {year}");
                        continue;
                    }
                    
                    // Upewnij się, że mamy obiekt miesiąca
                    if (!months.ContainsKey(month))
                    {
                        months[month] = new JObject
                        {
                            ["min"] = float.MaxValue,
                            ["max"] = float.MinValue,
                            ["days"] = new JObject()
                        };
                    }
                    
                    JObject? monthData = months[month] as JObject;
                    if (monthData == null) 
                    {
                        this.ServerApi.Logger.Error($"[TemperatureMonitor] Nie można utworzyć obiektu miesiąca: {month}");
                        continue;
                    }
                    
                    JObject? days = monthData["days"] as JObject;
                    if (days == null) 
                    {
                        this.ServerApi.Logger.Error($"[TemperatureMonitor] Nie można utworzyć obiektu dni dla miesiąca: {month}");
                        continue;
                    }
                    
                    // Dodaj dane dnia
                    days[day] = new JObject
                    {
                        ["min"] = minTemp,
                        ["max"] = maxTemp
                    };
                    
                    // Aktualizuj min/max dla miesiąca
                    JToken? monthMinToken = monthData["min"];
                    JToken? monthMaxToken = monthData["max"];
                    
                    float monthMin = monthMinToken != null ? monthMinToken.Value<float>() : float.MaxValue;
                    float monthMax = monthMaxToken != null ? monthMaxToken.Value<float>() : float.MinValue;
                    
                    if (minTemp < monthMin) monthData["min"] = minTemp;
                    if (maxTemp > monthMax) monthData["max"] = maxTemp;
                    
                    // Aktualizuj min/max dla roku
                    JToken? yearMinToken = yearData["min"];
                    JToken? yearMaxToken = yearData["max"];
                    
                    float yearMin = yearMinToken != null ? yearMinToken.Value<float>() : float.MaxValue;
                    float yearMax = yearMaxToken != null ? yearMaxToken.Value<float>() : float.MinValue;
                    
                    if (minTemp < yearMin) yearData["min"] = minTemp;
                    if (maxTemp > yearMax) yearData["max"] = maxTemp;
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
                    
                    // Utwórz kopię zapasową istniejącego pliku
                    if (File.Exists(filePath))
                    {
                        string backupPath = filePath + ".bak";
                        try
                        {
                            File.Copy(filePath, backupPath, true);
                            this.ServerApi.Logger.Debug($"[TemperatureMonitor] Utworzono kopię zapasową: {backupPath}");
                        }
                        catch (Exception ex)
                        {
                            this.ServerApi.Logger.Error($"[TemperatureMonitor] Błąd tworzenia kopii zapasowej: {ex.Message}");
                        }
                    }
                    
                    this.ServerApi.Logger.Notification($"[TemperatureMonitor] Zapisywanie do pliku: {filePath}");
                    
                    // Zapisz dane do tymczasowego pliku
                    string tempFilePath = filePath + ".tmp";
                    File.WriteAllText(tempFilePath, temperatureData.ToString(Formatting.Indented));
                    
                    // Sprawdź czy tymczasowy plik istnieje i ma zawartość
                    if (File.Exists(tempFilePath) && new FileInfo(tempFilePath).Length > 0)
                    {
                        // Sprawdź poprawność JSON w tymczasowym pliku
                        try
                        {
                            string tempContent = File.ReadAllText(tempFilePath);
                            JObject.Parse(tempContent); // Sprawdź czy można sparsować
                            
                            // Jeśli parsowanie się powiodło, przenieś plik na właściwe miejsce
                            if (File.Exists(filePath))
                            {
                                File.Delete(filePath);
                            }
                            File.Move(tempFilePath, filePath);
                            
                            this.ServerApi.Logger.Notification($"[TemperatureMonitor] Plik został pomyślnie zapisany.");
                            
                            // Zaloguj fragment zapisanych danych
                            string savedContent = File.ReadAllText(filePath);
                            this.ServerApi.Logger.Debug($"[TemperatureMonitor] Rozmiar zapisanych danych: {savedContent.Length} bajtów.");
                        }
                        catch (Exception ex)
                        {
                            this.ServerApi.Logger.Error($"[TemperatureMonitor] Błąd weryfikacji JSON: {ex.Message}");
                        }
                    }
                    else
                    {
                        this.ServerApi.Logger.Error($"[TemperatureMonitor] BŁĄD: Tymczasowy plik nie istnieje lub jest pusty!");
                    }
                    
                    // Sprawdź czy plik istnieje po zapisie
                    if (File.Exists(filePath))
                    {
                        FileInfo fi = new FileInfo(filePath);
                        this.ServerApi.Logger.Notification($"[TemperatureMonitor] Plik został pomyślnie zapisany. Rozmiar: {fi.Length} bajtów");
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
                if (ClientApi == null)
                {
                    Console.WriteLine("[TemperatureMonitor] Error: ClientApi is null");
                    return false;
                }
                
                if (translation == null)
                {
                    ClientApi.Logger.Error("[TemperatureMonitor] Error: translation is null");
                    ClientApi.ShowChatMessage("Error: translation system not initialized");
                    return false;
                }
                
                ClientApi.Logger.Debug("[TemperatureMonitor] Alt+T pressed, checking network channel");
                
                var channel = ClientApi.Network.GetChannel("temperaturemonitor");
                if (channel == null)
                {
                    ClientApi.Logger.Error("[TemperatureMonitor] Error: Network channel 'temperaturemonitor' not found");
                    ClientApi.ShowChatMessage("Error: Network communication channel not available");
                    return false;
                }
                
                // ClientApi.ShowChatMessage(translation.Get("hotkey_detected"));
                
                ClientApi.Logger.Debug("[TemperatureMonitor] Sending temperature data request to server");
                // Zamiast próbować czytać plik lokalnie, wysyłamy żądanie do serwera
                channel.SendPacket(new TemperatureDataRequest());
                
                return true;
            }
            catch (Exception ex)
            {
                if (ClientApi != null)
                {
                    ClientApi.Logger.Error($"[TemperatureMonitor] Error in OnToggleTemperatureDialog: {ex.Message}\n{ex.StackTrace}");
                    ClientApi.ShowChatMessage($"Error: {ex.Message}");
                }
                else
                {
                    Console.WriteLine($"[TemperatureMonitor] Critical error: {ex.Message}");
                }
            }
            
            return false;
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
                    ServerApi.Logger.Debug($"[TemperatureMonitor] Wczytano plik JSON, struktura główna zawiera {temperatureData.Count} elementów");
                    
                    // Iteracja po latach
                    foreach (var yearProp in temperatureData.Properties())
                    {
                        string year = yearProp.Name;
                        JObject? yearObj = yearProp.Value as JObject;
                        
                        if (yearObj == null) continue;
                        
                        // Iteracja po miesiącach
                        JObject? months = yearObj["months"] as JObject;
                        if (months != null)
                        {
                            ServerApi.Logger.Debug($"[TemperatureMonitor] Rok {year} zawiera {months.Count} miesięcy");
                            
                            foreach (var monthProp in months.Properties())
                            {
                                string month = monthProp.Name;
                                JObject? monthObj = monthProp.Value as JObject;
                                
                                if (monthObj == null) continue;
                                
                                // Iteracja po dniach
                                JObject? days = monthObj["days"] as JObject;
                                if (days != null)
                                {
                                    ServerApi.Logger.Debug($"[TemperatureMonitor] Miesiąc {month} roku {year} zawiera {days.Count} dni");
                                    
                                    foreach (var dayProp in days.Properties())
                                    {
                                        string day = dayProp.Name;
                                        JObject? dayObj = dayProp.Value as JObject;
                                        
                                        if (dayObj == null) continue;
                                        
                                        // Formatuj datę jako "rok-miesiąc-dzień"
                                        string formattedDate = $"{year}-{month}-{day.PadLeft(2, '0')}";
                                        
                                        if (dayObj.TryGetValue("min", out JToken? minToken) && 
                                            dayObj.TryGetValue("max", out JToken? maxToken))
                                        {
                                            if (minToken != null && maxToken != null && 
                                                minToken.Type != JTokenType.Null && maxToken.Type != JTokenType.Null)
                                            {
                                                float min = (float)minToken.ToObject<double>();
                                                float max = (float)maxToken.ToObject<double>();
                                                
                                                minTemperatures[formattedDate] = min;
                                                maxTemperatures[formattedDate] = max;
                                                
                                                ServerApi.Logger.Debug($"[TemperatureMonitor] Wczytano temperaturę dla {formattedDate}: min={min:F1}°C, max={max:F1}°C");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    ServerApi.Logger.Notification($"[TemperatureMonitor] Wczytano {minTemperatures.Count} zapisów temperatur z pliku.");
                    if (minTemperatures.Count > 0)
                    {
                        var keysArray = minTemperatures.Keys.ToArray();
                        string keysToShow = string.Join(", ", keysArray.Length > 10 ? keysArray.Take(10) : keysArray);
                        ServerApi.Logger.Debug($"[TemperatureMonitor] Wczytane daty: {keysToShow}" + 
                            (keysArray.Length > 10 ? $" i {keysArray.Length - 10} więcej..." : ""));
                    }
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
                // Dodaj dodatkowy log
                ServerApi.Logger.Notification($"[TemperatureMonitor] Zamykanie - zapisywanie {minTemperatures.Count} temperatur");
                SaveTemperatureData();
            }
            
            // Zwolnij zasoby interfejsu ImGui
            temperatureImGuiDialog?.Dispose();
            
            base.Dispose();
        }
     }
}