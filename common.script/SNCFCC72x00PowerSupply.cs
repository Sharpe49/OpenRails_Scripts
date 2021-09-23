// COPYRIGHT 2020 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using Orts.Common;
using Orts.Simulation;
using ORTS.Scripting.Api;
using System.Globalization;

namespace ORTS.Scripting.Script
{
    /// <summary>
    /// Power supply for French Diesel locomotives with no MU control and no dynamic braking.
    /// For example, it is appropriate for BB 67000, CC 72000 and CC 72100 locomotives.
    /// </summary>
    public class SNCFCC72x00PowerSupply : DieselPowerSupply
    {
        private Timer PowerOnTimer;
        private Timer AuxPowerOnTimer;

        private DieselEngineState PreviousFirstEngineState;
        private DieselEngineState PreviousSecondEngineState;

        private bool QuickPowerOn = false;

        public override void Initialize()
        {
            PowerOnTimer = new Timer(this);
            PowerOnTimer.Setup(PowerOnDelayS());

            AuxPowerOnTimer = new Timer(this);
            AuxPowerOnTimer.Setup(AuxPowerOnDelayS());

            PreviousFirstEngineState = CurrentDieselEngineState(0);
            PreviousSecondEngineState = CurrentDieselEngineState(1);
        }

        public override void Update(float elapsedClockSeconds)
        {
            SetCurrentBatteryState(BatterySwitchOn() ? PowerSupplyState.PowerOn : PowerSupplyState.PowerOff);
            SetCurrentLowVoltagePowerSupplyState(BatterySwitchOn() ? PowerSupplyState.PowerOn : PowerSupplyState.PowerOff);
            SetCurrentCabPowerSupplyState(BatterySwitchOn() && MasterKeyOn() ? PowerSupplyState.PowerOn : PowerSupplyState.PowerOff);

            switch (CurrentDieselEnginesState())
            {
                case DieselEngineState.Stopped:
                case DieselEngineState.Stopping:
                case DieselEngineState.Starting:
                    if (PowerOnTimer.Started)
                        PowerOnTimer.Stop();
                    if (AuxPowerOnTimer.Started)
                        AuxPowerOnTimer.Stop();

                    if (CurrentMainPowerSupplyState() == PowerSupplyState.PowerOn)
                    {
                        SignalEvent(Event.EnginePowerOff);
                        SetCurrentMainPowerSupplyState(PowerSupplyState.PowerOff);
                    }
                    SetCurrentAuxiliaryPowerSupplyState(PowerSupplyState.PowerOff);
                    break;

                case DieselEngineState.Running:
                    switch (CurrentTractionCutOffRelayState())
                    {
                        case TractionCutOffRelayState.Open:
                        case TractionCutOffRelayState.Closing:
                            // If traction cut-off relay is open, then it must be closed to finish the quick power-on sequence
                            if (QuickPowerOn)
                            {
                                QuickPowerOn = false;
                                SignalEventToTractionCutOffRelay(PowerSupplyEvent.QuickPowerOn);
                            }

                            if (PowerOnTimer.Started)
                                PowerOnTimer.Stop();

                            if (CurrentMainPowerSupplyState() == PowerSupplyState.PowerOn)
                            {
                                SetCurrentMainPowerSupplyState(PowerSupplyState.PowerOff);
                            }
                            break;

                        case TractionCutOffRelayState.Closed:
                            // If traction cut-off relay is closed, quick power-on sequence has finished
                            QuickPowerOn = false;

                            if (!PowerOnTimer.Started)
                                PowerOnTimer.Start();

                            if (PowerOnTimer.Triggered && CurrentMainPowerSupplyState() == PowerSupplyState.PowerOff)
                            {
                                SetCurrentMainPowerSupplyState(PowerSupplyState.PowerOn);
                            }
                            break;
                    }

                    if (!AuxPowerOnTimer.Started)
                        AuxPowerOnTimer.Start();

                    SetCurrentAuxiliaryPowerSupplyState(AuxPowerOnTimer.Triggered ? PowerSupplyState.PowerOn : PowerSupplyState.PowerOff);
                    break;
            }

            SetCurrentDynamicBrakeAvailability(false);

            if (ElectricTrainSupplyUnfitted())
            {
                SetCurrentElectricTrainSupplyState(PowerSupplyState.Unavailable);
            }
            else if (CurrentAuxiliaryPowerSupplyState() == PowerSupplyState.PowerOn
                    && ElectricTrainSupplySwitchOn())
            {
                SetCurrentElectricTrainSupplyState(PowerSupplyState.PowerOn);
            }
            else
            {
                SetCurrentElectricTrainSupplyState(PowerSupplyState.PowerOff);
            }

            UpdateSounds();
        }

        protected void UpdateSounds()
        {
            // First engine
            if ((PreviousFirstEngineState == DieselEngineState.Stopped
                || PreviousFirstEngineState == DieselEngineState.Stopping)
                && (CurrentDieselEngineState(0) == DieselEngineState.Starting
                || CurrentDieselEngineState(0) == DieselEngineState.Running))
            {
                SignalEvent(Event.EnginePowerOn);
            }
            else if ((PreviousFirstEngineState == DieselEngineState.Starting
                || PreviousFirstEngineState == DieselEngineState.Running)
                && (CurrentDieselEngineState(0) == DieselEngineState.Stopping
                || CurrentDieselEngineState(0) == DieselEngineState.Stopped))
            {
                SignalEvent(Event.EnginePowerOff);
            }
            PreviousFirstEngineState = CurrentDieselEngineState(0);

            // Second engine
            if ((PreviousSecondEngineState == DieselEngineState.Stopped
                || PreviousSecondEngineState == DieselEngineState.Stopping)
                && (CurrentDieselEngineState(1) == DieselEngineState.Starting
                || CurrentDieselEngineState(1) == DieselEngineState.Running))
            {
                SignalEvent(Event.SecondEnginePowerOn);
            }
            else if ((PreviousSecondEngineState == DieselEngineState.Starting
                || PreviousSecondEngineState == DieselEngineState.Running)
                && (CurrentDieselEngineState(1) == DieselEngineState.Stopping
                || CurrentDieselEngineState(1) == DieselEngineState.Stopped))
            {
                SignalEvent(Event.SecondEnginePowerOff);
            }
            PreviousSecondEngineState = CurrentDieselEngineState(1);
        }

        public override void HandleEvent(PowerSupplyEvent evt)
        {
            switch (evt)
            {
                case PowerSupplyEvent.CloseTractionCutOffRelayButtonPressed:
                    if (!MasterKeyOn())
                    {
                        if (CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "fr")
                        {
                            Message(ConfirmLevel.Warning, "Ce bouton ne peut être utilisé que si la boîte à leviers est déverrouillée");
                        }
                        else
                        {
                            Message(ConfirmLevel.Warning, "This button can only be used when the master key is turned on");
                        }
                    }
                    else if (ThrottlePercent() > 0)
                    {
                        if (CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "fr")
                        {
                            Message(ConfirmLevel.Warning, "Ce bouton ne peut être utilisé que si le manipulateur traction est à 0");
                        }
                        else
                        {
                            Message(ConfirmLevel.Warning, "This button can only be used when the throttle controller is at 0");
                        }
                    }
                    else
                    {
                        SignalEventToTractionCutOffRelay(evt);
                        SignalEventToTcs(evt);
                    }
                    break;

                case PowerSupplyEvent.CloseTractionCutOffRelayButtonReleased:
                    SignalEventToTractionCutOffRelay(evt);
                    SignalEventToTcs(evt);
                    break;

                case PowerSupplyEvent.SwitchOnElectricTrainSupply:
                    if (MasterKeyOn())
                    {
                        SignalEventToElectricTrainSupplySwitch(evt);
                        SignalEventToTcs(evt);
                    }
                    else
                    {
                        if (CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "fr")
                        {
                            Message(ConfirmLevel.Warning, "Ce bouton ne peut être utilisé que si la boîte à leviers est déverrouillée");
                        }
                        else
                        {
                            Message(ConfirmLevel.Warning, "This button can only be used when the master key is turned on");
                        }
                    }
                    break;

                case PowerSupplyEvent.SwitchOffElectricTrainSupply:
                    SignalEventToElectricTrainSupplySwitch(evt);
                    SignalEventToTcs(evt);
                    break;

                case PowerSupplyEvent.CloseBatterySwitch:
                case PowerSupplyEvent.CloseBatterySwitchButtonPressed:
                case PowerSupplyEvent.CloseBatterySwitchButtonReleased:
                case PowerSupplyEvent.OpenBatterySwitch:
                case PowerSupplyEvent.OpenBatterySwitchButtonPressed:
                case PowerSupplyEvent.OpenBatterySwitchButtonReleased:
                    SignalEventToBatterySwitch(evt);
                    SignalEventToTcs(evt);
                    break;

                case PowerSupplyEvent.TurnOnMasterKey:
                case PowerSupplyEvent.TurnOffMasterKey:
                    if (!TractionCutOffRelayDriverClosingAuthorization() && !TractionCutOffRelayDriverClosingOrder() && !TractionCutOffRelayDriverOpeningOrder() && !ElectricTrainSupplySwitchOn())
                    {
                        SignalEventToMasterKey(evt);
                        SignalEventToTcs(evt);
                    }
                    else
                    {
                        if (CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "fr")
                        {
                            Message(ConfirmLevel.Warning, "Tous les leviers de la rangée supérieure de la boîte à leviers doivent être bas (sauf le bouton de maintien de service) pour verrouiller la boîte à leviers");
                        }
                        else
                        {
                            Message(ConfirmLevel.Warning, "All upper levers must be down (except service hold button) in order to switch off the master key");
                        }
                    }
                    break;

                case PowerSupplyEvent.QuickPowerOn:
                    QuickPowerOn = true;
                    SignalEventToBatterySwitch(PowerSupplyEvent.CloseBatterySwitch);
                    SignalEventToMasterKey(PowerSupplyEvent.TurnOnMasterKey);
                    SignalEventToDieselEngines(PowerSupplyEvent.StartEngine);
                    SignalEventToElectricTrainSupplySwitch(PowerSupplyEvent.SwitchOnElectricTrainSupply);
                    break;

                case PowerSupplyEvent.QuickPowerOff:
                    QuickPowerOn = false;
                    SignalEventToElectricTrainSupplySwitch(PowerSupplyEvent.SwitchOffElectricTrainSupply);
                    SignalEventToTractionCutOffRelay(PowerSupplyEvent.OpenTractionCutOffRelay);
                    SignalEventToDieselEngines(PowerSupplyEvent.StopEngine);
                    SignalEventToMasterKey(PowerSupplyEvent.TurnOffMasterKey);
                    SignalEventToBatterySwitch(PowerSupplyEvent.OpenBatterySwitch);
                    break;

                case PowerSupplyEvent.TogglePlayerEngine:
                    switch (CurrentDieselEngineState(0))
                    {
                        case DieselEngineState.Stopped:
                        case DieselEngineState.Stopping:
                            SignalEventToDieselEngine(PowerSupplyEvent.StartEngine, 0);
                            Confirm(CabControl.PlayerDiesel, CabSetting.On);
                            break;

                        case DieselEngineState.Starting:
                            SignalEventToDieselEngine(PowerSupplyEvent.StopEngine, 0);
                            Confirm(CabControl.PlayerDiesel, CabSetting.Off);
                            break;

                        case DieselEngineState.Running:
                            if (ThrottlePercent() < 1)
                            {
                                SignalEventToDieselEngine(PowerSupplyEvent.StopEngine, 0);
                                Confirm(CabControl.PlayerDiesel, CabSetting.Off);
                            }
                            else
                            {
                                Confirm(CabControl.PlayerDiesel, CabSetting.Warn1);
                            }
                            break;
                    }
                    break;
            }
        }

        public override void HandleEventFromLeadLocomotive(PowerSupplyEvent evt) { }
    }

}