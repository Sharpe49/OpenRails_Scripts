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

using System;
using System.Globalization;
using Orts.Common;
using Orts.Simulation;
using ORTS.Common;
using ORTS.Scripting.Api;

namespace ORTS.Scripting.Script
{
    /// <summary>
    /// Power supply for French high speed train.
    /// </summary>
    public class SNCFTGVPowerSupplyM2 : ElectricPowerSupply
    {
        [Flags]
        public enum PowerSupplyData
        {
            None = 0,
            M1Leading = 1,
            M2Leading = 2,
            Panto0 = 4,
            PantoN = 8,
            PantoS = 16,
            PantoL = 32,
            PowerLimitationI = 64,
            PowerLimitationII = 128,
            PowerLimitationIII = 256,
            Shoes750V = 512,
            Panto1500V = 1024,
            Panto3000V_B = 2048,
            Panto3000V_FS = 4096,
            Panto15000V_D = 8192, // In Deutschland, 15000V on 1950mm pantograph (1500V pantograph)
            Panto15000V_CH = 16384, // In Switzerland, 15000V on 1450mm pantograph (25000V pantograph)
            Panto25000V = 32768,
            Panto25000V_ET = 65536,
            Panto25000V_LGV = 131072,
            ServiceRetention = 262144,
            ElectricTrainSupply = 524288,
        }

        public enum LocomotiveNumber
        {
            M1,
            M2,
        }

        public enum PantographMode
        {
            Zero,
            Normal,
            Secours,
            Local,
        }

        public enum PantographVoltage
        {
            None,
            Shoes750V,
            Panto1500V,
            Panto3000V_B,
            Panto3000V_FS,
            Panto15000V_D, // In Deutschland, 15000V on 1950mm pantograph (1500V pantograph)
            Panto15000V_CH, // In Switzerland, 15000V on 1450mm pantograph (25000V pantograph)
            Panto25000V,
            Panto25000V_ET,
            Panto25000V_LGV,
        }

        public enum PowerLimitation
        {
            I,
            II,
            III,
        }

        public enum TgvType
        {
            PSE,
            Atlantique,
            Reseau,
            ReseauDuplex,
            Duplex,
            PBKA,
            POS,
            DASYE,
            RGV2N2,
        }

        public new float LineVoltageV { get; set; } = 0f;

        public LocomotiveNumber LocomotiveLeading = LocomotiveNumber.M2;
        public PantographMode SelectedPantographMode = PantographMode.Zero;
        public PantographVoltage SelectedPantographVoltage = PantographVoltage.None;
        public PowerLimitation SelectedPowerLimitation = PowerLimitation.II;
        
        public TgvType TrainType;

        public bool LowerPantograph = false;
        public PantographVoltage LowerPantographNewVoltage = PantographVoltage.None;

        private IIRFilter PantographFilter;
        private IIRFilter VoltageFilter;
        private Timer PowerOnTimer;
        private Timer AuxPowerOnTimer;

        private Timer InitTimer;
        private bool Init = true;

        private bool QuickPowerOn = false;
        private bool RaisePantograph1 = false; // 1950mm pantograph : 1500V, 3000V Belgium and 15000V Germany
        private bool RaisePantograph2 = false; // 1450mm pantograph : 25000V, 3000V Italy and 15000V Switzerland
        private bool LowerShoes = false;
        private bool RoofLineConnected = false;
        private bool ElectricTrainSupplyActive = false;

        private PantographMode ServiceRetentionPantographMode = PantographMode.Zero;
        private PantographVoltage ServiceRetentionPantographVoltage = PantographVoltage.None;
        private bool ServiceRetentionElectricTrainSupply = false;

        public override void Initialize()
        {
            PantographFilter = new IIRFilter(IIRFilter.FilterTypes.Butterworth, 1, IIRFilter.HzToRad(0.1f), 0.001f);
            VoltageFilter = new IIRFilter(IIRFilter.FilterTypes.Butterworth, 1, IIRFilter.HzToRad(0.1f), 0.001f);

            PowerOnTimer = new Timer(this);
            PowerOnTimer.Setup(PowerOnDelayS());

            AuxPowerOnTimer = new Timer(this);
            AuxPowerOnTimer.Setup(AuxPowerOnDelayS());

            InitTimer = new Timer(this);
            InitTimer.Setup(5);

            TrainType = (TgvType)Enum.Parse(typeof(TgvType), GetStringParameter("PowerSupply", "TrainType", "Duplex"));

            LocomotiveLeading = IsLocomotiveLeading ? LocomotiveNumber.M1 : LocomotiveNumber.M2;
            ServiceRetentionActive = true;
            ServiceRetentionPantographMode = PantographMode.Normal;
            ServiceRetentionPantographVoltage = LineVoltageV() >= 25000f ? PantographVoltage.Panto25000V : PantographVoltage.Panto1500V;
            ServiceRetentionElectricTrainSupply = true;

            SelectedPantographMode = ServiceRetentionPantographMode;
            SelectedPantographVoltage = ServiceRetentionPantographVoltage;

            SetCurrentBatteryState(PowerSupplyState.PowerOn);
            SetCurrentLowVoltagePowerSupplyState(PowerSupplyState.PowerOn);
            SetCurrentMainPowerSupplyState(PowerSupplyState.PowerOn);
            SetCurrentAuxiliaryPowerSupplyState(PowerSupplyState.PowerOn);
            SetCurrentElectricTrainSupplyState(PowerSupplyState.PowerOn);
            SetPantographVoltageV(LineVoltageV());
            SetFilterVoltageV(PantographVoltageV());

            UpdatePantographCommand();
        }

        public override void InitializeMoving()
        {
            ServiceRetentionActive = false;
        }

        public override void Update(float elapsedClockSeconds)
        {
            if (Init)
            {
                if (!InitTimer.Started)
                {
                    InitTimer.Start();
                    SignalEvent(Event.EnginePowerOn);
                }

                if (InitTimer.Triggered)
                {
                    InitTimer.Stop();
                    Init = false;
                }

                return;
            }

            SetCurrentBatteryState(BatterySwitchOn() ? PowerSupplyState.PowerOn : PowerSupplyState.PowerOff);
            SetCurrentLowVoltagePowerSupplyState(BatterySwitchOn() ? PowerSupplyState.PowerOn : PowerSupplyState.PowerOff);
            SetCurrentCabPowerSupplyState(BatterySwitchOn() && MasterKeyOn() ? PowerSupplyState.PowerOn : PowerSupplyState.PowerOff);

            if (IsLocomotiveLeading && CurrentCabPowerSupplyState() == PowerSupplyState.PowerOn)
            {
                LocomotiveLeading = LocomotiveNumber.M2;
            }

            UpdateServiceRetention();
            UpdatePantographCommand();
            UpdatePowerLimitation();

            UpdateLineVoltage();

            switch (CurrentPantographState())
            {
                case PantographState.Down:
                case PantographState.Lowering:
                case PantographState.Raising:
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
                    SetCurrentElectricTrainSupplyState(PowerSupplyState.PowerOff);
                    SetPantographVoltageV(PantographFilter.Filter(0.0f, elapsedClockSeconds));
                    SetFilterVoltageV(VoltageFilter.Filter(0.0f, elapsedClockSeconds));
                    break;

                case PantographState.Up:
                    SetPantographVoltageV(PantographFilter.Filter(LineVoltageV, elapsedClockSeconds));

                    switch (CurrentCircuitBreakerState())
                    {
                        case CircuitBreakerState.Open:
                        case CircuitBreakerState.Closing:
                            // If circuit breaker is open, then it must be closed to finish the quick power-on sequence
                            if (QuickPowerOn)
                            {
                                QuickPowerOn = false;
                                SignalEventToCircuitBreaker(PowerSupplyEvent.QuickPowerOn);
                            }

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
                            SetFilterVoltageV(VoltageFilter.Filter(0.0f, elapsedClockSeconds));
                            break;

                        case CircuitBreakerState.Closed:
                            // If circuit breaker is closed, quick power-on sequence has finished
                            QuickPowerOn = false;

                            if (!PowerOnTimer.Started)
                                PowerOnTimer.Start();
                            if (!AuxPowerOnTimer.Started)
                                AuxPowerOnTimer.Start();

                            if (PowerOnTimer.Triggered && CurrentMainPowerSupplyState() == PowerSupplyState.PowerOff)
                            {
                                SignalEvent(Event.EnginePowerOn);
                                SetCurrentMainPowerSupplyState(PowerSupplyState.PowerOn);
                            }
                            SetCurrentAuxiliaryPowerSupplyState(AuxPowerOnTimer.Triggered ? PowerSupplyState.PowerOn : PowerSupplyState.PowerOff);
                            SetFilterVoltageV(VoltageFilter.Filter(PantographVoltageV(), elapsedClockSeconds));
                            break;
                    }
                    break;
            }

            switch (SelectedPantographVoltage)
            {
                case PantographVoltage.Shoes750V:
                case PantographVoltage.Panto1500V:
                case PantographVoltage.Panto3000V_B:
                case PantographVoltage.Panto3000V_FS:
                    PantographVoltageVAC = 0f;
                    PantographVoltageVDC = PantographVoltageV();
                    break;

                case PantographVoltage.Panto15000V_D:
                case PantographVoltage.Panto15000V_CH:
                case PantographVoltage.Panto25000V:
                case PantographVoltage.Panto25000V_ET:
                case PantographVoltage.Panto25000V_LGV:
                    PantographVoltageVAC = PantographVoltageV();
                    PantographVoltageVDC = 0f;
                    break;

                case PantographVoltage.None:
                default:
                    PantographVoltageVAC = 0f;
                    PantographVoltageVDC = 0f;
                    break;
            }

            SetCurrentDynamicBrakeAvailability(true);

            UpdateElectricTrainSupply();

            if (IsLocomotiveLeading)
            {
                SignalEventToOtherLocomotivesWithId(PowerSupplyEvent.RaisePantograph, Serialize());
            }
        }

        protected void UpdateServiceRetention()
        {
            if (!IsLocomotiveLeading && ServiceRetentionButton)
            {
                ServiceRetentionButton = false;
            }

            if (CurrentCabPowerSupplyState() != PowerSupplyState.PowerOn
                && ServiceRetentionActive
                && ServiceRetentionCancellationButton)
            {
                ServiceRetentionActive = false;
                SignalEventToBatterySwitch(PowerSupplyEvent.OpenBatterySwitchRequestedByPowerSupply);
                SignalEventToOtherTrainVehicles(PowerSupplyEvent.OpenBatterySwitchRequestedByPowerSupply);
            }
            else if (CurrentCabPowerSupplyState() == PowerSupplyState.PowerOn
                     && IsLocomotiveLeading
                     && LocomotiveLeading == LocomotiveNumber.M2)
            {
                ServiceRetentionActive = ServiceRetentionButton;
            }
        }

        protected void UpdateLineVoltage()
        {
            switch (CurrentPantographState())
            {
                case PantographState.Down:
                case PantographState.Lowering:
                case PantographState.Raising:
                    LineVoltageV = 0f;
                    break;

                case PantographState.Up:
                    switch (SelectedPantographVoltage)
                    {
                        case PantographVoltage.Shoes750V:
                            LineVoltageV = 750f;
                            break;

                        case PantographVoltage.Panto1500V:
                            LineVoltageV = 1500f;
                            break;

                        case PantographVoltage.Panto3000V_B:
                        case PantographVoltage.Panto3000V_FS:
                            LineVoltageV = 3000f;
                            break;

                        case PantographVoltage.Panto15000V_D:
                        case PantographVoltage.Panto15000V_CH:
                            LineVoltageV = 15000f;
                            break;

                        case PantographVoltage.Panto25000V:
                        case PantographVoltage.Panto25000V_ET:
                        case PantographVoltage.Panto25000V_LGV:
                            LineVoltageV = 25000f;
                            break;

                        case PantographVoltage.None:
                        default:
                            LineVoltageV = 0f;
                            break;
                    }
                    break;
            }
        }

        protected void UpdatePantographCommand()
        {
            bool raisePantograph1 = false;
            bool raisePantograph2 = false;
            bool lowerShoes = false;
            bool roofLineConnected = false;

            if (IsLocomotiveLeading && LocomotiveLeading == LocomotiveNumber.M2)
            {
                if (CurrentCabPowerSupplyState() != PowerSupplyState.PowerOn
                    || LowerPantograph
                    || LowerPantographNewVoltage != PantographVoltage.None)
                {
                    SelectedPantographMode = PantographMode.Zero;
                }
                else
                {
                    switch (PantographSelectorPosition.Name)
                    {
                        case "0":
                            SelectedPantographMode = PantographMode.Zero;
                            break;

                        case "N":
                            SelectedPantographMode = PantographMode.Normal;
                            break;

                        case "S":
                            SelectedPantographMode = PantographMode.Secours;
                            break;

                        case "L":
                            SelectedPantographMode = PantographMode.Local;
                            break;
                    }
                }

                if (CurrentCabPowerSupplyState() != PowerSupplyState.PowerOn)
                {
                    SelectedPantographVoltage = PantographVoltage.None;
                }
                else
                {
                    switch (VoltageSelectorPosition.Name)
                    {
                        case "M":
                        case "~F":
                        case "~L":
                        case "~BR":
                            SelectedPantographVoltage = PantographVoltage.Panto25000V;

                            if (LowerPantographNewVoltage == PantographVoltage.Panto25000V)
                            {
                                LowerPantographNewVoltage = PantographVoltage.None;
                            }

                            break;

                        case "LGV":
                        case "~GV":
                        case "~HS":
                        case "~STI":
                            SelectedPantographVoltage = PantographVoltage.Panto25000V_LGV;

                            if (LowerPantographNewVoltage == PantographVoltage.Panto25000V_LGV)
                            {
                                LowerPantographNewVoltage = PantographVoltage.None;
                            }

                            break;

                        case "~ET":
                            SelectedPantographVoltage = PantographVoltage.Panto25000V_ET;

                            if (LowerPantographNewVoltage == PantographVoltage.Panto25000V_ET)
                            {
                                LowerPantographNewVoltage = PantographVoltage.None;
                            }

                            break;

                        case "C":
                        case "=F":
                            SelectedPantographVoltage = PantographVoltage.Panto1500V;

                            if (LowerPantographNewVoltage == PantographVoltage.Panto1500V)
                            {
                                LowerPantographNewVoltage = PantographVoltage.None;
                            }

                            break;

                        case "3KV":
                            SelectedPantographVoltage = PantographVoltage.Panto3000V_B;

                            if (LowerPantographNewVoltage == PantographVoltage.Panto3000V_B)
                            {
                                LowerPantographNewVoltage = PantographVoltage.None;
                            }

                            break;

                        case "=FS":
                            SelectedPantographVoltage = PantographVoltage.Panto3000V_FS;
                            break;

                        case "~D":
                            SelectedPantographVoltage = PantographVoltage.Panto15000V_D;
                            break;

                        case "~CH":
                            SelectedPantographVoltage = PantographVoltage.Panto15000V_CH;
                            break;

                        case "=BR":
                            SelectedPantographVoltage = PantographVoltage.Shoes750V;
                            break;
                    }
                }

                if (CurrentCabPowerSupplyState() != PowerSupplyState.PowerOn)
                {
                    if (!ServiceRetentionActive)
                    {
                        ServiceRetentionPantographMode = PantographMode.Zero;
                        ServiceRetentionPantographVoltage = PantographVoltage.None;
                        ServiceRetentionElectricTrainSupply = false;
                    }

                    SelectedPantographMode = ServiceRetentionPantographMode;
                    SelectedPantographVoltage = ServiceRetentionPantographVoltage;
                }
                else
                {
                    if (ServiceRetentionActive)
                    {
                        if (ServiceRetentionPantographMode == PantographMode.Zero &&
                            SelectedPantographMode == PantographMode.Normal)
                        {
                            ServiceRetentionPantographMode = PantographMode.Normal;
                        }

                        if (ServiceRetentionPantographVoltage == PantographVoltage.None)
                        {
                            ServiceRetentionPantographVoltage = SelectedPantographVoltage;
                        }

                        if (!ServiceRetentionElectricTrainSupply && ElectricTrainSupplySwitchOn())
                        {
                            ServiceRetentionElectricTrainSupply = true;
                        }

                        SelectedPantographMode = ServiceRetentionPantographMode;
                        SelectedPantographVoltage = ServiceRetentionPantographVoltage;
                    }
                    else
                    {
                        ServiceRetentionPantographMode = PantographMode.Zero;
                        ServiceRetentionPantographVoltage = PantographVoltage.None;
                        ServiceRetentionElectricTrainSupply = false;
                    }
                }
            }

            if (CurrentLowVoltagePowerSupplyState() != PowerSupplyState.PowerOn)
            {
                SelectedPantographMode = PantographMode.Zero;
                SelectedPantographVoltage = PantographVoltage.None;
            }

            switch (SelectedPantographVoltage)
            {
                case PantographVoltage.Panto25000V:
                case PantographVoltage.Panto25000V_ET:
                case PantographVoltage.Panto25000V_LGV:
                case PantographVoltage.Panto15000V_CH:
                    switch (SelectedPantographMode)
                    {
                        case PantographMode.Zero:
                            raisePantograph1 = false;
                            raisePantograph2 = false;
                            lowerShoes = false;
                            roofLineConnected = false;
                            break;

                        case PantographMode.Normal:
                            raisePantograph1 = false;
                            raisePantograph2 = LocomotiveLeading == LocomotiveNumber.M1;
                            lowerShoes = false;
                            roofLineConnected = true;
                            break;

                        case PantographMode.Secours:
                            raisePantograph1 = false;
                            raisePantograph2 = LocomotiveLeading == LocomotiveNumber.M2;
                            lowerShoes = false;
                            roofLineConnected = true;
                            break;

                        case PantographMode.Local:
                            raisePantograph1 = false;
                            raisePantograph2 = IsLocomotiveLeading;
                            lowerShoes = false;
                            roofLineConnected = false;
                            break;
                    }
                    break;

                case PantographVoltage.Panto1500V:
                case PantographVoltage.Panto3000V_B:
                    switch (SelectedPantographMode)
                    {
                        case PantographMode.Zero:
                            raisePantograph1 = false;
                            raisePantograph2 = false;
                            lowerShoes = false;
                            roofLineConnected = false;
                            break;

                        case PantographMode.Normal:
                        case PantographMode.Secours:
                            raisePantograph1 = true;
                            raisePantograph2 = false;
                            lowerShoes = false;
                            roofLineConnected = false;
                            break;

                        case PantographMode.Local:
                            raisePantograph1 = IsLocomotiveLeading;
                            raisePantograph2 = false;
                            lowerShoes = false;
                            roofLineConnected = false;
                            break;
                    }
                    break;

                case PantographVoltage.Panto3000V_FS:
                    switch (SelectedPantographMode)
                    {
                        case PantographMode.Zero:
                            raisePantograph1 = false;
                            raisePantograph2 = false;
                            lowerShoes = false;
                            roofLineConnected = false;
                        break;

                        case PantographMode.Normal:
                        case PantographMode.Secours:
                            raisePantograph1 = false;
                            raisePantograph2 = true;
                            lowerShoes = false;
                            roofLineConnected = false;
                        break;

                        case PantographMode.Local:
                            raisePantograph1 = false;
                            raisePantograph2 = IsLocomotiveLeading;
                            lowerShoes = false;
                            roofLineConnected = false;
                            break;
                    }
                    break;

                case PantographVoltage.Panto15000V_D:
                    switch (SelectedPantographMode)
                    {
                        case PantographMode.Zero:
                            raisePantograph1 = false;
                            raisePantograph2 = false;
                            lowerShoes = false;
                            roofLineConnected = false;
                        break;

                        case PantographMode.Normal:
                            raisePantograph1 = LocomotiveLeading == LocomotiveNumber.M1;
                            raisePantograph2 = false;
                            lowerShoes = false;
                            roofLineConnected = true;
                        break;

                        case PantographMode.Secours:
                            raisePantograph1 = LocomotiveLeading == LocomotiveNumber.M2;
                            raisePantograph2 = false;
                            lowerShoes = false;
                            roofLineConnected = true;
                        break;

                        case PantographMode.Local:
                            raisePantograph1 = IsLocomotiveLeading;
                            raisePantograph2 = false;
                            lowerShoes = false;
                            roofLineConnected = false;
                            break;
                    }
                    break;

                case PantographVoltage.Shoes750V:
                    switch (SelectedPantographMode)
                    {
                        case PantographMode.Zero:
                            raisePantograph1 = false;
                            raisePantograph2 = false;
                            lowerShoes = false;
                            roofLineConnected = false;
                        break;

                        case PantographMode.Normal:
                        case PantographMode.Secours:
                            raisePantograph1 = false;
                            raisePantograph2 = false;
                            lowerShoes = true;
                            roofLineConnected = false;
                        break;

                        case PantographMode.Local:
                            raisePantograph1 = false;
                            raisePantograph2 = false;
                            lowerShoes = IsLocomotiveLeading;
                            roofLineConnected = false;
                            break;
                    }
                    break;

                default:
                    raisePantograph1 = false;
                    raisePantograph2 = false;
                    lowerShoes = false;
                    roofLineConnected = false;
                    break;
            }

            if (RaisePantograph1 != raisePantograph1)
            {
                SignalEventToPantograph(raisePantograph1 ? PowerSupplyEvent.RaisePantograph : PowerSupplyEvent.LowerPantograph, 1);
                RaisePantograph1 = raisePantograph1;
            }

            if (RaisePantograph2 != raisePantograph2)
            {
                SignalEventToPantograph(raisePantograph2 ? PowerSupplyEvent.RaisePantograph : PowerSupplyEvent.LowerPantograph, 2);
                RaisePantograph2 = raisePantograph2;
            }

            if (LowerShoes != lowerShoes)
            {
                SignalEventToPantograph(lowerShoes ? PowerSupplyEvent.RaisePantograph : PowerSupplyEvent.LowerPantograph, 4);
                LowerShoes = lowerShoes;
            }

            if (RoofLineConnected != roofLineConnected)
            {
                SignalEventToPantograph(roofLineConnected ? PowerSupplyEvent.RaisePantograph : PowerSupplyEvent.LowerPantograph, 3);
                RoofLineConnected = roofLineConnected;
            }
        }

        protected void UpdatePowerLimitation()
        {
            if (IsLocomotiveLeading && LocomotiveLeading == LocomotiveNumber.M2)
            {
                switch (PowerLimitationSelectorPosition.Name)
                {
                    case "I":
                        SelectedPowerLimitation = PowerLimitation.I;
                        break;

                    case "II":
                        SelectedPowerLimitation = PowerLimitation.II;
                        break;

                    case "III":
                        SelectedPowerLimitation = PowerLimitation.III;
                        break;
                }
            }

            switch (TrainType)
            {
                case TgvType.Atlantique:
                case TgvType.Reseau:
                case TgvType.ReseauDuplex:
                case TgvType.Duplex:
                case TgvType.PBKA:
                    switch (SelectedPantographVoltage)
                    {
                        case PantographVoltage.Panto1500V:
                        case PantographVoltage.Panto3000V_B:
                        case PantographVoltage.Panto3000V_FS:
                            switch (SelectedPowerLimitation)
                            {
                                case PowerLimitation.I when NumberOfLocomotives() <= 2:
                                    MaximumPowerW = 1000000;
                                    break;

                                case PowerLimitation.I when NumberOfLocomotives() > 2:
                                    MaximumPowerW = 600000;
                                    break;

                                case PowerLimitation.II when NumberOfLocomotives() <= 2:
                                    MaximumPowerW = 1440000;
                                    break;

                                case PowerLimitation.II when NumberOfLocomotives() > 2:
                                    MaximumPowerW = 1000000;
                                    break;

                                case PowerLimitation.III when NumberOfLocomotives() <= 2:
                                    MaximumPowerW = 1940000;
                                    break;

                                case PowerLimitation.III when NumberOfLocomotives() > 2:
                                    MaximumPowerW = 1440000;
                                    break;
                            }
                            break;

                        case PantographVoltage.Panto15000V_D:
                            switch (SelectedPowerLimitation)
                            {
                                case PowerLimitation.I when NumberOfLocomotives() <= 2:
                                    MaximumPowerW = 1680000;
                                    break;

                                case PowerLimitation.I when NumberOfLocomotives() > 2:
                                    MaximumPowerW = 1000000;
                                    break;

                                case PowerLimitation.II when NumberOfLocomotives() <= 2:
                                    MaximumPowerW = 2200000;
                                    break;

                                case PowerLimitation.II when NumberOfLocomotives() > 2:
                                    MaximumPowerW = 1680000;
                                    break;

                                case PowerLimitation.III when NumberOfLocomotives() <= 2:
                                    MaximumPowerW = 2580000;
                                    break;

                                case PowerLimitation.III when NumberOfLocomotives() > 2:
                                    MaximumPowerW = 2200000;
                                    break;
                            }
                            break;

                        case PantographVoltage.Panto25000V:
                            switch (SelectedPowerLimitation)
                            {
                                case PowerLimitation.I when NumberOfLocomotives() <= 2:
                                    MaximumPowerW = 1680000;
                                    break;

                                case PowerLimitation.I when NumberOfLocomotives() > 2:
                                    MaximumPowerW = 1000000;
                                    break;

                                case PowerLimitation.II when NumberOfLocomotives() <= 2:
                                    MaximumPowerW = 2200000;
                                    break;

                                case PowerLimitation.II when NumberOfLocomotives() > 2:
                                    MaximumPowerW = 1680000;
                                    break;

                                case PowerLimitation.III when NumberOfLocomotives() <= 2:
                                    MaximumPowerW = 3200000;
                                    break;

                                case PowerLimitation.III when NumberOfLocomotives() > 2:
                                    MaximumPowerW = 2200000;
                                    break;
                            }
                            break;

                        case PantographVoltage.Panto25000V_LGV:
                            switch (SelectedPowerLimitation)
                            {
                                case PowerLimitation.I when NumberOfLocomotives() <= 2:
                                    MaximumPowerW = 2200000;
                                    break;

                                case PowerLimitation.I when NumberOfLocomotives() > 2:
                                    MaximumPowerW = 1680000;
                                    break;

                                case PowerLimitation.II when NumberOfLocomotives() <= 2:
                                    MaximumPowerW = 3200000;
                                    break;

                                case PowerLimitation.II when NumberOfLocomotives() > 2:
                                    MaximumPowerW = 2200000;
                                    break;

                                case PowerLimitation.III when NumberOfLocomotives() <= 2:
                                    MaximumPowerW = 4400000;
                                    break;

                                case PowerLimitation.III when NumberOfLocomotives() > 2:
                                    MaximumPowerW = 3200000;
                                    break;
                            }
                            break;
                    }
                    break;
            }
        }

        protected void UpdateElectricTrainSupply()
        {
            if (IsLocomotiveLeading && LocomotiveLeading == LocomotiveNumber.M2)
            {
                ElectricTrainSupplyActive = ElectricTrainSupplySwitchOn() || ServiceRetentionElectricTrainSupply;
            }

            if (ElectricTrainSupplyUnfitted())
            {
                SetCurrentElectricTrainSupplyState(PowerSupplyState.Unavailable);
            }
            else if (CurrentAuxiliaryPowerSupplyState() == PowerSupplyState.PowerOn
                     && ElectricTrainSupplyActive)
            {
                SetCurrentElectricTrainSupplyState(PowerSupplyState.PowerOn);
            }
            else
            {
                SetCurrentElectricTrainSupplyState(PowerSupplyState.PowerOff);
            }
        }

        protected int Serialize()
        {
            PowerSupplyData data = PowerSupplyData.None;

            switch (LocomotiveLeading)
            {
                case LocomotiveNumber.M1:
                    data |= PowerSupplyData.M1Leading;
                    break;

                case LocomotiveNumber.M2:
                    data |= PowerSupplyData.M2Leading;
                    break;
            }

            switch (SelectedPantographMode)
            {
                case PantographMode.Zero:
                    data |= PowerSupplyData.Panto0;
                    break;

                case PantographMode.Normal:
                    data |= PowerSupplyData.PantoN;
                    break;

                case PantographMode.Secours:
                    data |= PowerSupplyData.PantoS;
                    break;

                case PantographMode.Local:
                    data |= PowerSupplyData.PantoL;
                    break;
            }

            switch (SelectedPantographVoltage)
            {
                case PantographVoltage.Shoes750V:
                    data |= PowerSupplyData.Shoes750V;
                    break;

                case PantographVoltage.Panto1500V:
                    data |= PowerSupplyData.Panto1500V;
                    break;

                case PantographVoltage.Panto3000V_B:
                    data |= PowerSupplyData.Panto3000V_B;
                    break;

                case PantographVoltage.Panto3000V_FS:
                    data |= PowerSupplyData.Panto3000V_FS;
                    break;

                case PantographVoltage.Panto15000V_D:
                    data |= PowerSupplyData.Panto15000V_D;
                    break;

                case PantographVoltage.Panto15000V_CH:
                    data |= PowerSupplyData.Panto15000V_CH;
                    break;

                case PantographVoltage.Panto25000V:
                    data |= PowerSupplyData.Panto25000V;
                    break;

                case PantographVoltage.Panto25000V_ET:
                    data |= PowerSupplyData.Panto25000V_ET;
                    break;

                case PantographVoltage.Panto25000V_LGV:
                    data |= PowerSupplyData.Panto25000V_LGV;
                    break;
            }

            switch (SelectedPowerLimitation)
            {
                case PowerLimitation.I:
                    data |= PowerSupplyData.PowerLimitationI;
                    break;

                case PowerLimitation.II:
                    data |= PowerSupplyData.PowerLimitationII;
                    break;

                case PowerLimitation.III:
                    data |= PowerSupplyData.PowerLimitationIII;
                    break;
            }

            if (ServiceRetentionActive)
            {
                data |= PowerSupplyData.ServiceRetention;
            }

            if (ElectricTrainSupplyActive)
            {
                data |= PowerSupplyData.ElectricTrainSupply;
            }

            return (int)data;
        }

        protected void Deserialize(int receivedData)
        {
            PowerSupplyData data = (PowerSupplyData)receivedData;

            if (data.HasFlag(PowerSupplyData.M1Leading))
            {
                LocomotiveLeading = LocomotiveNumber.M1;
            }
            else if (data.HasFlag(PowerSupplyData.M2Leading))
            {
                LocomotiveLeading = LocomotiveNumber.M2;
            }

            if (data.HasFlag(PowerSupplyData.Panto0))
            {
                SelectedPantographMode = PantographMode.Zero;
            }
            else if (data.HasFlag(PowerSupplyData.PantoN))
            {
                SelectedPantographMode = PantographMode.Normal;
            }
            else if (data.HasFlag(PowerSupplyData.PantoS))
            {
                SelectedPantographMode = PantographMode.Secours;
            }
            else if (data.HasFlag(PowerSupplyData.PantoL))
            {
                SelectedPantographMode = PantographMode.Local;
            }

            if (data.HasFlag(PowerSupplyData.Shoes750V))
            {
                SelectedPantographVoltage = PantographVoltage.Shoes750V;
            }
            else if (data.HasFlag(PowerSupplyData.Panto1500V))
            {
                SelectedPantographVoltage = PantographVoltage.Panto1500V;
            }
            else if (data.HasFlag(PowerSupplyData.Panto3000V_B))
            {
                SelectedPantographVoltage = PantographVoltage.Panto3000V_B;
            }
            else if (data.HasFlag(PowerSupplyData.Panto3000V_FS))
            {
                SelectedPantographVoltage = PantographVoltage.Panto3000V_FS;
            }
            else if (data.HasFlag(PowerSupplyData.Panto15000V_D))
            {
                SelectedPantographVoltage = PantographVoltage.Panto15000V_D;
            }
            else if (data.HasFlag(PowerSupplyData.Panto15000V_CH))
            {
                SelectedPantographVoltage = PantographVoltage.Panto15000V_CH;
            }
            else if (data.HasFlag(PowerSupplyData.Panto25000V))
            {
                SelectedPantographVoltage = PantographVoltage.Panto25000V;
            }
            else if (data.HasFlag(PowerSupplyData.Panto25000V_ET))
            {
                SelectedPantographVoltage = PantographVoltage.Panto25000V_ET;
            }
            else if (data.HasFlag(PowerSupplyData.Panto25000V_LGV))
            {
                SelectedPantographVoltage = PantographVoltage.Panto25000V_LGV;
            }

            if (data.HasFlag(PowerSupplyData.PowerLimitationI))
            {
                SelectedPowerLimitation = PowerLimitation.I;
            }
            else if (data.HasFlag(PowerSupplyData.PowerLimitationII))
            {
                SelectedPowerLimitation = PowerLimitation.II;
            }
            else if (data.HasFlag(PowerSupplyData.PowerLimitationIII))
            {
                SelectedPowerLimitation = PowerLimitation.III;
            }

            ServiceRetentionActive = data.HasFlag(PowerSupplyData.ServiceRetention);

            ElectricTrainSupplyActive = data.HasFlag(PowerSupplyData.ElectricTrainSupply);
        }

        public override void HandleEvent(PowerSupplyEvent evt)
        {
            switch (evt)
            {
                case PowerSupplyEvent.IncreaseVoltageSelectorPosition:
                case PowerSupplyEvent.DecreaseVoltageSelectorPosition:
                    SignalEventToVoltageSelector(evt);
                    break;

                case PowerSupplyEvent.IncreasePantographSelectorPosition:
                case PowerSupplyEvent.DecreasePantographSelectorPosition:
                    SignalEventToPantographSelector(evt);
                    break;

                case PowerSupplyEvent.IncreasePowerLimitationSelectorPosition:
                case PowerSupplyEvent.DecreasePowerLimitationSelectorPosition:
                    SignalEventToPowerLimitationSelector(evt);
                    break;

                case PowerSupplyEvent.GiveCircuitBreakerClosingAuthorization:
                    if (MasterKeyOn())
                    {
                        SignalEventToCircuitBreaker(evt);
                        SignalEventToTcs(evt);
                        SignalEventToOtherTrainVehicles(evt);
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

                case PowerSupplyEvent.CloseCircuitBreakerButtonPressed:
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
                        SignalEventToCircuitBreaker(evt);
                        SignalEventToTcs(evt);
                        SignalEventToOtherTrainVehicles(evt);
                    }
                    break;

                case PowerSupplyEvent.CloseCircuitBreakerButtonReleased:
                case PowerSupplyEvent.RemoveCircuitBreakerClosingAuthorization:
                    SignalEventToCircuitBreaker(evt);
                    SignalEventToTcs(evt);
                    SignalEventToOtherTrainVehicles(evt);
                    break;

                case PowerSupplyEvent.ServiceRetentionButtonPressed:
                    if (!ServiceRetentionButton)
                    {
                        ServiceRetentionButton = true;
                        SignalEvent(Event.ServiceRetentionButtonOn);
                        SignalEventToCircuitBreaker(evt);
                    }
                    break;

                case PowerSupplyEvent.ServiceRetentionButtonReleased:
                    if (ServiceRetentionButton)
                    {
                        ServiceRetentionButton = false;
                        SignalEvent(Event.ServiceRetentionButtonOff);
                        SignalEventToCircuitBreaker(evt);
                    }
                    break;

                case PowerSupplyEvent.ServiceRetentionCancellationButtonPressed:
                    if (!ServiceRetentionCancellationButton)
                    {
                        ServiceRetentionCancellationButton = true;
                        SignalEvent(Event.ServiceRetentionCancellationButtonOn);
                        SignalEventToCircuitBreaker(evt);
                    }
                    break;

                case PowerSupplyEvent.ServiceRetentionCancellationButtonReleased:
                    if (ServiceRetentionCancellationButton)
                    {
                        ServiceRetentionCancellationButton = false;
                        SignalEvent(Event.ServiceRetentionCancellationButtonOff);
                        SignalEventToCircuitBreaker(evt);
                    }
                    break;

                case PowerSupplyEvent.SwitchOnElectricTrainSupply:
                    if (MasterKeyOn())
                    {
                        SignalEventToElectricTrainSupplySwitch(evt);
                        SignalEventToTcs(evt);
                        SignalEventToOtherTrainVehicles(evt);
                    }
                    else
                    {
                        if (CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "fr")
                        {
                            Message(ConfirmLevel.Warning, "Cet interrupteur ne peut être utilisé que si la boîte à leviers est déverrouillée");
                        }
                        else
                        {
                            Message(ConfirmLevel.Warning, "This switch can only be used when the master key is turned on");
                        }
                    }
                    break;

                case PowerSupplyEvent.SwitchOffElectricTrainSupply:
                    SignalEventToElectricTrainSupplySwitch(evt);
                    SignalEventToTcs(evt);
                    SignalEventToOtherTrainVehicles(evt);
                    break;

                case PowerSupplyEvent.CloseBatterySwitch:
                case PowerSupplyEvent.CloseBatterySwitchButtonPressed:
                case PowerSupplyEvent.CloseBatterySwitchButtonReleased:
                case PowerSupplyEvent.OpenBatterySwitch:
                case PowerSupplyEvent.OpenBatterySwitchButtonPressed:
                case PowerSupplyEvent.OpenBatterySwitchButtonReleased:
                    SignalEventToBatterySwitch(evt);
                    SignalEventToTcs(evt);
                    SignalEventToOtherTrainVehicles(evt);
                    break;

                case PowerSupplyEvent.TurnOnMasterKey:
                case PowerSupplyEvent.TurnOffMasterKey:
                    if (!CircuitBreakerDriverClosingAuthorization() && !CircuitBreakerDriverClosingOrder() && !CircuitBreakerDriverOpeningOrder() && !ElectricTrainSupplySwitchOn())
                    {
                        if (CurrentLowVoltagePowerSupplyState() == PowerSupplyState.PowerOn)
                        {
                            SignalEventToMasterKey(evt);
                            SignalEventToTcs(evt);
                        }
                        else
                        {
                            if (CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "fr")
                            {
                                Message(ConfirmLevel.Warning, "Les batteries doivent être connectée avant de déverrouiller la boîte à leviers");
                            }
                            else
                            {
                                Message(ConfirmLevel.Warning, "Batteries must be connected before switching on the master key");
                            }
                        }
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
                    SignalEventToPantograph(PowerSupplyEvent.RaisePantograph, 1);
                    SignalEventToElectricTrainSupplySwitch(PowerSupplyEvent.SwitchOnElectricTrainSupply);

                    SignalEventToOtherTrainVehicles(PowerSupplyEvent.QuickPowerOn);
                    break;

                case PowerSupplyEvent.QuickPowerOff:
                    QuickPowerOn = false;
                    SignalEventToElectricTrainSupplySwitch(PowerSupplyEvent.SwitchOffElectricTrainSupply);
                    SignalEventToCircuitBreaker(PowerSupplyEvent.QuickPowerOff);
                    SignalEventToPantographs(PowerSupplyEvent.LowerPantograph);
                    SignalEventToMasterKey(PowerSupplyEvent.TurnOffMasterKey);
                    SignalEventToBatterySwitch(PowerSupplyEvent.OpenBatterySwitch);

                    SignalEventToOtherTrainVehicles(PowerSupplyEvent.QuickPowerOff);
                    break;
            }
        }

        public override void HandleEventFromTcs(PowerSupplyEvent evt, string message)
        {
            switch (evt)
            {
                case PowerSupplyEvent.LowerPantograph:
                    LowerPantograph = true;

                    switch (message)
                    {
                        case "ELC1_5":
                            LowerPantographNewVoltage = PantographVoltage.Panto1500V;
                            break;

                        case "ELC3":
                            LowerPantographNewVoltage = PantographVoltage.Panto3000V_B;
                            break;

                        case "ELC25":
                            LowerPantographNewVoltage = PantographVoltage.Panto25000V;
                            break;

                        case "ELGV":
                            LowerPantographNewVoltage = PantographVoltage.Panto25000V_LGV;
                            break;

                        case "EET":
                            LowerPantographNewVoltage = PantographVoltage.Panto25000V_ET;
                            break;
                    }
                    break;

                case PowerSupplyEvent.RaisePantograph:
                    LowerPantograph = false;
                    break;
            }
        }

        public override void HandleEventFromLeadLocomotive(PowerSupplyEvent evt)
        {
            switch (evt)
            {
                case PowerSupplyEvent.CloseCircuitBreakerButtonPressed:
                case PowerSupplyEvent.CloseCircuitBreakerButtonReleased:
                case PowerSupplyEvent.GiveCircuitBreakerClosingAuthorization:
                case PowerSupplyEvent.RemoveCircuitBreakerClosingAuthorization:
                    SignalEventToCircuitBreaker(evt);
                    SignalEventToTcs(evt);
                    break;

                case PowerSupplyEvent.SwitchOnElectricTrainSupply:
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
                case PowerSupplyEvent.OpenBatterySwitchRequestedByPowerSupply:
                    SignalEventToBatterySwitch(evt);
                    SignalEventToTcs(evt);
                    break;

                case PowerSupplyEvent.QuickPowerOn:
                    QuickPowerOn = true;
                    SignalEventToBatterySwitch(PowerSupplyEvent.CloseBatterySwitch);
                    SignalEventToPantograph(PowerSupplyEvent.RaisePantograph, 1);
                    SignalEventToElectricTrainSupplySwitch(PowerSupplyEvent.SwitchOnElectricTrainSupply);
                    break;

                case PowerSupplyEvent.QuickPowerOff:
                    QuickPowerOn = false;
                    SignalEventToElectricTrainSupplySwitch(PowerSupplyEvent.SwitchOffElectricTrainSupply);
                    SignalEventToCircuitBreaker(PowerSupplyEvent.QuickPowerOff);
                    SignalEventToPantographs(PowerSupplyEvent.LowerPantograph);
                    SignalEventToBatterySwitch(PowerSupplyEvent.OpenBatterySwitch);
                    break;
            }
        }

        public override void HandleEvent(PowerSupplyEvent evt, int id)
        {
            switch (evt)
            {
                case PowerSupplyEvent.RaisePantograph:
                case PowerSupplyEvent.LowerPantograph:
                    SignalEventToVoltageSelector(evt, id);
                    SignalEventToPantographSelector(evt, id);
                    break;
            }
        }

        public override void HandleEventFromLeadLocomotive(PowerSupplyEvent evt, int id)
        {
            switch (evt)
            {
                case PowerSupplyEvent.RaisePantograph:
                case PowerSupplyEvent.LowerPantograph:
                    Deserialize(id);
                    break;
            }
        }
    }

}