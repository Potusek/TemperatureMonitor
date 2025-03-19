using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace TemperatureMonitor
{
    public class GuiDialogTemperature : GuiDialog
    {
        private readonly Translation translation;
        
        // Dodajemy implementację abstrakcyjnej właściwości
        public override string ToggleKeyCombinationCode => "temperaturehistory";
        
        public GuiDialogTemperature(ICoreClientAPI capi, Translation translation) : base(capi)
        {
            this.translation = translation;
            
            try
            {
                // Najprostsze możliwe okno
                ElementBounds dialogBounds = ElementBounds.Fixed(EnumDialogArea.CenterMiddle, 0, 0, 300, 200);
                ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
                
                SingleComposer = capi.Gui
                    .CreateCompo("temperaturehistory", dialogBounds)
                    .AddShadedDialogBG(bgBounds)
                    .AddDialogTitleBar("Temperature Test", OnTitleBarClose)
                    .BeginChildElements(bgBounds)
                        .AddStaticText("Simple test dialog", CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, 0, 200, 50))
                    .EndChildElements()
                    .Compose();
                
                capi.Logger.Debug("[TemperatureMonitor] Simple dialog created successfully");
            }
            catch (Exception ex)
            {
                capi.Logger.Error($"[TemperatureMonitor] Error creating dialog: {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void OnTitleBarClose()
        {
            TryClose();
        }
    }
}