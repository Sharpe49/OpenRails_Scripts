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

using Orts.Simulation;
using Orts.Simulation.RollingStocks;
using ORTS.Common;
using ORTS.Scripting.Api;
using System;
using System.Collections.Generic;

namespace ORTS.Scripting.Script
{
    public class TCS_France : TrainControlSystem
    {
        // Cabview control number
        const int BP_AC_SF = 0;
        const int BP_A_LS_SF = 2;
        const int Z_ES_VA = 3;
        const int BP_AM_V1 = 9;
        const int BP_AM_V2 = 10;
        const int BP_DM = 11;
        const int LS_SF = 32;
        const int VY_SOS_RSO = 33;
        const int VY_SOS_VAC = 34;
        const int VY_ES_FU = 35;
        const int VY_SOS_KVB = 36;
        const int VY_VTE = 37;
        const int VY_FU = 38;
        const int TVM_Mask = 47;

        enum ETCSLevel
        {
            L0,         // Unfitted (national system active)
            NTC,        // Specific Transmission Module (national system information transmitted to ETCS)
            L1,         // Level 1 : Beacon transmission, loop transmission and radio in-fill
            L2,         // Level 2 : Radio transmission, beacon positionning
            L3          // Level 3 : Same as level 2 + moving block
        }

        enum RSOStateType
        {
            Init,
            Off,
            TriggeredPressed,
            TriggeredBlinking,
            TriggeredFixed
        }

        enum KVBStateType
        {
            Normal,
            Alert,
            Emergency
        }

        enum KVBPreAnnounceType
        {
            Deactivated,
            Armed,
            Triggered
        }

        enum KVBModeType
        {
            ConventionalLine,
            HighSpeedLine,
            Shunting
        }

        enum KVBReleaseSpeed
        {
            V30,
            V10
        }

        ETCSLevel CurrentETCSLevel = ETCSLevel.L0;

    // Properties
        bool RearmingButton
        {
            get
            {
                if (Locomotive() is MSTSElectricLocomotive)
                {
                    MSTSElectricLocomotive electricLocomotive = Locomotive() as MSTSElectricLocomotive;

                    return electricLocomotive.PowerSupply.CircuitBreaker.DriverClosingOrder;
                }
                else
                {
                    return true;
                }
            }
        }

    // Train parameters
        bool VACMAPresent;                                  // VACMA
        bool RSOPresent;                                    // RSO
        bool DAATPresent;                                   // DAAT
        bool KVBPresent;                                    // KVB
        bool TVM300Present;                                 // TVM300
        bool TVM430Present;                                 // TVM430
        bool ETCSPresent;                                   // ETCS (Not implemented)
        ETCSLevel ETCSMaxLevel = ETCSLevel.L0;              // ETCS maximum level (Not implemented)
        bool ElectroPneumaticBrake;                         // EP
        bool HeavyFreightTrain;                             // MA train only
        float SafeDecelerationMpS2;                         // Gamma

    // RSO (Répétition Optique des Signaux / Optical Signal Repetition)
        // Parameters
        float RSODelayBeforeEmergencyBrakingS;
        float RSOBlinkerFrequencyHz;

        // Variables
        RSOStateType RSOState = RSOStateType.Init;
        Aspect RSOLastSignalAspect = Aspect.Clear_1;
        bool RSOEmergencyBraking = true;
        bool RSOPressed = false;
        bool RSOPreviousPressed = false;
        bool RSOCancelPressed = false;
        bool RSOType1Inhibition = false;                    // Inhibition 1 : Reverse
        bool RSOType2Inhibition = false;                    // Inhibition 2 : KVB not inhibited and train on HSL
        bool RSOType3Inhibition = false;                    // Inhibition 3 : TVM COVIT not inhibited
        bool RSOClosedSignal = false;
        bool RSOPreviousClosedSignal = false;
        bool RSOOpenedSignal = false;
        Blinker RSOBlinker;
        Timer RSOEmergencyTimer;

    // DAAT (Dispositif d'Arrêt Automatique des Trains / Automatic Train Stop System)
        // Not implemented

    // KVB (Contrôle de Vitesse par Balises / Beacon speed control)
        // Parameters
        bool KVBInhibited;
        const float KVBDelayBeforeEmergencyBrakingS = 5f;   // Tx
        float KVBTrainSpeedLimitMpS;                        // VT
        float KVBTrainLengthM;                              // L
        float KVBDelayBeforeBrakingEstablishedS;            // Tbo

        // Variables
        bool KVBSpadEmergency = false;
        bool KVBOverspeedEmergency = false;
        bool KVBKarmEmergency = false;
        KVBStateType KVBState = KVBStateType.Emergency;
        bool KVBEmergencyBraking = true;
        KVBPreAnnounceType KVBPreAnnounce = KVBPreAnnounceType.Deactivated;
        KVBModeType KVBMode = KVBModeType.ConventionalLine;

        OdoMeter KVBHSLStartOdometer;

        Aspect KVBLastSignalAspect = Aspect.Clear_1;
        float KVBLastSignalSpeedLimitMpS = float.PositiveInfinity;

        int KVBStopTargetSignalNumber = -1;
        float KVBStopTargetDistanceM = float.PositiveInfinity;
        KVBReleaseSpeed KVBStopTargetReleaseSpeed = KVBReleaseSpeed.V30;
        bool KVBOnSight = false;

        int KVBSpeedRestrictionTargetSignalNumber = -1;
        float KVBSpeedRestrictionTargetDistanceM = float.PositiveInfinity;
        float KVBSpeedRestrictionTargetSpeedMpS = float.PositiveInfinity;

        float KVBDeclivity = 0f;                            // i

        float KVBCurrentLineSpeedLimitMpS = float.PositiveInfinity;
        float KVBNextLineSpeedLimitMpS = float.PositiveInfinity;
        float KVBNextLineSpeedDistanceM = float.PositiveInfinity;

        bool KVBSpeedTooHighLight = false;
        bool KVBEmergencyBrakeLight = false;

    // TVM COVIT common
        // Parameters
        bool TVMCOVITInhibited = false;

        // Variables
        bool TVMArmed = false;
        bool TVMCOVITEmergencyBraking = false;

        Aspect TVMAspect;
        Aspect TVMPreviousAspect;
        bool TVMClosedSignal;
        bool TVMPreviousClosedSignal;
        bool TVMOpenedSignal;
        bool TVMPreviousOpenedSignal;

    // TVM300 COVIT (Transmission Voie Machine 300 COntrôle de VITesse / Track Machine Transmission 300 Speed control)
        // Constants
        Dictionary<Aspect, float> TVM300CurrentSpeedLimitsKph = new Dictionary<Aspect, float>
        {
            {Aspect.None, 300f},
            {Aspect.Clear_2, 300f},
            {Aspect.Clear_1, 300f},
            {Aspect.Approach_3, 270f},
            {Aspect.Approach_2, 270f},
            {Aspect.Approach_1, 220f},
            {Aspect.Restricted, 220f},
            {Aspect.StopAndProceed, 160f},
            {Aspect.Stop, 160f},
            {Aspect.Permission, 30f}
        };
        Dictionary<Aspect, float> TVM300NextSpeedLimitsKph = new Dictionary<Aspect, float>
        {
            {Aspect.None, 300f},
            {Aspect.Clear_2, 300f},
            {Aspect.Clear_1, 270f},
            {Aspect.Approach_3, 270f},
            {Aspect.Approach_2, 220f},
            {Aspect.Approach_1, 220f},
            {Aspect.Restricted, 160f},
            {Aspect.StopAndProceed, 160f},
            {Aspect.Stop, 30f},
            {Aspect.Permission, 30f}
        };

        // Parameters
        float TVM300TrainSpeedLimitMpS;

        // Variables
        float TVM300CurrentSpeedLimitMpS;
        float TVM300NextSpeedLimitMpS;
        float TVM300EmergencySpeedMpS;

    // TVM430 COVIT (Transmission Voie Machine 430 COntrôle de VITesse / Track Machine Transmission 430 Speed control)
        // Constants
        // TVM430 300 km/h
        Dictionary<Aspect, float> TVM430S300CurrentSpeedLimitsKph = new Dictionary<Aspect, float>
        {
            {Aspect.None, 300f},
            {Aspect.Clear_2, 300f},
            {Aspect.Clear_1, 300f},
            {Aspect.Approach_3, 270f},
            {Aspect.Approach_2, 230f},
            {Aspect.Approach_1, 230f},
            {Aspect.Restricted, 170f},
            {Aspect.StopAndProceed, 170f},
            {Aspect.Stop, 170f},
            {Aspect.Permission, 30f}
        };
        Dictionary<Aspect, float> TVM430S300NextSpeedLimitsKph = new Dictionary<Aspect, float>
        {
            {Aspect.None, 300f},
            {Aspect.Clear_2, 300f},
            {Aspect.Clear_1, 270f},
            {Aspect.Approach_3, 230f},
            {Aspect.Approach_2, 170f},
            {Aspect.Approach_1, 160f},
            {Aspect.Restricted, 80f},
            {Aspect.StopAndProceed, 30f},
            {Aspect.Stop, 0f},
            {Aspect.Permission, 30f}
        };

        // TVM430 320 km/h
        Dictionary<Aspect, float> TVM430S320CurrentSpeedLimitsKph = new Dictionary<Aspect, float>
        {
            {Aspect.None, 320f},
            {Aspect.Clear_2, 320f},
            {Aspect.Clear_1, 320f},
            {Aspect.Approach_3, 300f},
            {Aspect.Approach_2, 270f},
            {Aspect.Approach_1, 230f},
            {Aspect.Restricted, 170f},
            {Aspect.StopAndProceed, 170f},
            {Aspect.Stop, 170f},
            {Aspect.Permission, 30f}
        };
        Dictionary<Aspect, float> TVM430S320NextSpeedLimitsKph = new Dictionary<Aspect, float>
        {
            {Aspect.None, 320f},
            {Aspect.Clear_2, 320f},
            {Aspect.Clear_1, 300f},
            {Aspect.Approach_3, 270f},
            {Aspect.Approach_2, 230f},
            {Aspect.Approach_1, 170f},
            {Aspect.Restricted, 80f},
            {Aspect.StopAndProceed, 30f},
            {Aspect.Stop, 0f},
            {Aspect.Permission, 30f}
        };

        // Parameters
        float TVM430TrainSpeedLimitMpS;

        // Variables
        Timer TVM430AspectChangeTimer;
        float TVM430CurrentSpeedLimitMpS;
        float TVM430CurrentEmergencySpeedMpS;
        float TVM430NextSpeedLimitMpS;
        float TVM430NextEmergencySpeedMpS;
        float TVM430EmergencyDecelerationMpS2;
        float TVM430ResetDecelerationMpS2;
        float TVM430EmergencySpeedCurveMpS;
        float TVM430ResetSpeedCurveMpS;

    // Vigilance monitoring (VACMA)
        // Parameters
        float VACMAActivationSpeedMpS;
        float VACMAReleasedAlertDelayS;
        float VACMAReleasedEmergencyDelayS;
        float VACMAPressedAlertDelayS;
        float VACMAPressedEmergencyDelayS;

        // Variables
        bool VACMAEmergencyBraking = true;
        bool VACMATest = false;
        bool VACMAPressed = false;
        Timer VACMAPressedAlertTimer;
        Timer VACMAPressedEmergencyTimer;
        Timer VACMAReleasedAlertTimer;
        Timer VACMAReleasedEmergencyTimer;

    // Other variables
        float InitCount = 0;

        bool EmergencyBraking = false;
        bool ExternalEmergencyBraking = false;

        float PreviousNormalSignalDistanceM = 0f;
        bool NormalSignalPassed = false;

        float PreviousDistantSignalDistanceM = 0f;
        bool DistantSignalPassed = false;

        float PreviousLineSpeed = 0f;

        public TCS_France() { }

        public override void Initialize()
        {
            // General section
            VACMAPresent = GetBoolParameter("General", "VACMAPresent", true);
            RSOPresent = GetBoolParameter("General", "RSOPresent", true);
            DAATPresent = GetBoolParameter("General", "DAATPresent", false);
            KVBPresent = GetBoolParameter("General", "KVBPresent", false);
            TVM300Present = GetBoolParameter("General", "TVM300Present", false);
            TVM430Present = GetBoolParameter("General", "TVM430Present", false);
            ETCSPresent = GetBoolParameter("General", "ETCSPresent", false);
            ElectroPneumaticBrake = GetBoolParameter("General", "ElectroPneumaticBrake", false);
            HeavyFreightTrain = GetBoolParameter("General", "HeavyFreightTrain", false);
            SafeDecelerationMpS2 = GetFloatParameter("General", "SafeDecelerationMpS2", 0.7f);

            // RSO section
            RSODelayBeforeEmergencyBrakingS = GetFloatParameter("RSO", "DelayBeforeEmergencyBrakingS", 4f);
            RSOBlinkerFrequencyHz = GetFloatParameter("RSO", "BlinkerFrequencyHz", 6f);

            // KVB section
            KVBInhibited = GetBoolParameter("KVB", "Inhibited", false);
            KVBTrainSpeedLimitMpS = MpS.FromKpH(GetFloatParameter("KVB", "TrainSpeedLimitKpH", 160f));

            KVBHSLStartOdometer = new OdoMeter(this);
            KVBHSLStartOdometer.Setup(450f);

            // TVM common section
            TVMCOVITInhibited = GetBoolParameter("TVM", "CovitInhibited", false);

            // TVM300 section
            TVM300TrainSpeedLimitMpS = MpS.FromKpH(GetFloatParameter("TVM300", "TrainSpeedLimitKpH", 300f));

            // TVM430 section
            TVM430TrainSpeedLimitMpS = MpS.FromKpH(GetFloatParameter("TVM430", "TrainSpeedLimitKpH", 320f));

            // VACMA section
            VACMAActivationSpeedMpS = MpS.FromKpH(GetFloatParameter("VACMA", "ActivationSpeedKpH", 3f));
            VACMAReleasedAlertDelayS = GetFloatParameter("VACMA", "ReleasedAlertDelayS", 2.5f);
            VACMAReleasedEmergencyDelayS = GetFloatParameter("VACMA", "ReleasedEmergencyDelayS", 5f);
            VACMAPressedAlertDelayS = GetFloatParameter("VACMA", "PressedAlertDelayS", 55f);
            VACMAPressedEmergencyDelayS = GetFloatParameter("VACMA", "PressedEmergencyDelayS", 60f);

            // Variables initialization
            RSOBlinker = new Blinker(this);
            RSOBlinker.Setup(RSOBlinkerFrequencyHz);
            RSOBlinker.Start();
            RSOEmergencyTimer = new Timer(this);
            RSOEmergencyTimer.Setup(RSODelayBeforeEmergencyBrakingS);

            VACMAPressedAlertTimer = new Timer(this);
            VACMAPressedAlertTimer.Setup(VACMAPressedAlertDelayS);
            VACMAPressedEmergencyTimer = new Timer(this);
            VACMAPressedEmergencyTimer.Setup(VACMAPressedEmergencyDelayS);
            VACMAReleasedAlertTimer = new Timer(this);
            VACMAReleasedAlertTimer.Setup(VACMAReleasedAlertDelayS);
            VACMAReleasedEmergencyTimer = new Timer(this);
            VACMAReleasedEmergencyTimer.Setup(VACMAReleasedEmergencyDelayS);

            TVM430AspectChangeTimer = new Timer(this);
            TVM430AspectChangeTimer.Setup(4.7f);

            // Cabview control names initialization
            SetCustomizedCabviewControlName(BP_AC_SF, "BP (AC) SF : Acquittement / Acknowledge");
            SetCustomizedCabviewControlName(BP_A_LS_SF, "BP (A) LS (SF) : Annulation LS (SF) / Cancel LS (SF)");
            SetCustomizedCabviewControlName(Z_ES_VA, "Z (ES) VA : Essai VACMA / Alerter test");
            SetCustomizedCabviewControlName(BP_AM_V1, "BP AM V1 : Armement manuel TVM voie 1 / TVM manual arming track 1");
            SetCustomizedCabviewControlName(BP_AM_V2, "BP AM V2 : Armement manuel TVM voie 2 / TVM manual arming track 2");
            SetCustomizedCabviewControlName(BP_DM, "BP DM : Désarmement manuel TVM / TVM manual dearming");
            SetCustomizedCabviewControlName(LS_SF, "LS (SF) : Signal Fermé / Closed Signal");
            SetCustomizedCabviewControlName(VY_SOS_RSO, "VY SOS RSO : FU RSO / RSO EB");
            SetCustomizedCabviewControlName(VY_SOS_VAC, "VY SOS VAC : FU VACMA / Alerter EB");
            SetCustomizedCabviewControlName(VY_ES_FU, "VY ES FU : Essai FU / EB test");
            SetCustomizedCabviewControlName(VY_SOS_KVB, "VY SOS KVB : FU KVB / KVB EB");
            SetCustomizedCabviewControlName(VY_VTE, "VY VTE : Vitesse Trop Elevée / Speed too high");
            SetCustomizedCabviewControlName(VY_FU, "VY FU : FU KVB / KVB EB");
            SetCustomizedCabviewControlName(TVM_Mask, "Masque TVM / TVM mask");

            Activated = true;

            SetNextSignalAspect(Aspect.Clear_1);
        }

        public override void InitializeMoving()
        {
            RSOState = RSOStateType.Off;
            RSOEmergencyBraking = false;
            KVBState = KVBStateType.Normal;
            KVBEmergencyBraking = false;
            VACMAEmergencyBraking = false;

            if (CurrentPostSpeedLimitMpS() > MpS.FromKpH(221f))
            {
                KVBMode = KVBModeType.HighSpeedLine;
                TVMArmed = true;
                UpdateTVMAspect(NextSignalAspect(0), false);
            }
        }

        public override void Update()
        {
            if (IsTrainControlEnabled())
            {
                if (InitCount < 5)
                {
                    InitCount++;

                    if (InitCount == 5 && CurrentPostSpeedLimitMpS() > MpS.FromKpH(221f))
                    {
                        KVBMode = KVBModeType.HighSpeedLine;
                    }

                    return;
                }

                UpdateSignalPassed();

                UpdateVACMA();
                UpdateRSO();
                UpdateTVM();
                UpdateKVB();

                if (RSOEmergencyBraking
                    || KVBEmergencyBraking
                    || TVMCOVITEmergencyBraking
                    || VACMAEmergencyBraking
                    || ExternalEmergencyBraking)
                {
                    EmergencyBraking = true;
                }
                else if (RearmingButton)
                {
                    EmergencyBraking = false;
                }

                SetEmergencyBrake(EmergencyBraking);

                SetPenaltyApplicationDisplay(IsBrakeEmergency());

                SetPowerAuthorization(!EmergencyBraking);

                RSOType1Inhibition = IsDirectionReverse();
                RSOType2Inhibition = !KVBInhibited && ((TVM300Present || TVM430Present) && TVMArmed);
                RSOType3Inhibition = (!TVM300Present && !TVM430Present) || !TVMCOVITInhibited;

                PreviousLineSpeed = CurrentPostSpeedLimitMpS();
            }
        }

        public override void SetEmergency(bool emergency)
        {
            ExternalEmergencyBraking = emergency;
        }

        protected void UpdateRSO()
        {
            if (RSOPresent && IsSpeedControlEnabled())
            {
                // If train is about to cross a normal signal, get its information.
                float nextNormalSignalDistance = NextSignalDistanceM(0);
                if (nextNormalSignalDistance <= 5f)
                {
                    RSOLastSignalAspect = NextSignalAspect(0);
                }

                // If train is about to cross a normal signal, get its information.
                float nextDistantSignalDistance = NextDistanceSignalDistanceM();
                if (nextDistantSignalDistance <= 5f)
                {
                    RSOLastSignalAspect = NextDistanceSignalAspect();
                }

                RSOClosedSignal = RSOOpenedSignal = false;

                if ((NormalSignalPassed || DistantSignalPassed)
                    && !RSOType1Inhibition
                    && !TVMArmed
                    && SpeedMpS() > 0.1f)
                {
                    if (RSOLastSignalAspect == Aspect.Stop
                        || RSOLastSignalAspect == Aspect.StopAndProceed
                        || RSOLastSignalAspect == Aspect.Restricted
                        || RSOLastSignalAspect == Aspect.Approach_1
                        || RSOLastSignalAspect == Aspect.Approach_2
                        || RSOLastSignalAspect == Aspect.Approach_3
                        )
                    {
                        RSOClosedSignal = true;
                    }
                    else
                    {
                        RSOOpenedSignal = true;
                    }
                }

                if ((RSOClosedSignal && !RSOType2Inhibition) || (TVMClosedSignal && !RSOType3Inhibition))
                {
                    RSOEmergencyTimer.Start();

                    if (RSOPressed)
                    {
                        RSOState = RSOStateType.TriggeredPressed;
                    }
                    else
                    {
                        RSOState = RSOStateType.TriggeredBlinking;
                    }
                }

                if (RSOOpenedSignal || TVMOpenedSignal || RSOCancelPressed)
                {
                    RSOEmergencyTimer.Stop();
                    RSOState = RSOStateType.Off;
                }

                switch (RSOState)
                {
                    case RSOStateType.Init:
                        if (!RSOBlinker.Started)
                        {
                            RSOBlinker.Start();
                        }
                        SetCabDisplayControl(LS_SF, RSOBlinker.On || RSOPressed ? 1 : 0);
                        break;

                    case RSOStateType.Off:
                        if (RSOBlinker.Started)
                        {
                            RSOBlinker.Stop();
                        }
                        SetCabDisplayControl(LS_SF, RSOPressed ? 1 : 0);
                        break;

                    case RSOStateType.TriggeredPressed:
                        SetCabDisplayControl(LS_SF, 0);

                        if (!RSOPressed)
                        {
                            RSOState = RSOStateType.TriggeredFixed;
                            RSOEmergencyTimer.Stop();
                        }
                        break;

                    case RSOStateType.TriggeredBlinking:
                        if (!RSOBlinker.Started)
                        {
                            RSOBlinker.Start();
                        }
                        SetCabDisplayControl(LS_SF, RSOBlinker.On ? 1 : 0);

                        if (!RSOPressed && RSOPreviousPressed)
                        {
                            RSOState = RSOStateType.TriggeredFixed;
                            RSOEmergencyTimer.Stop();
                        }
                        break;

                    case RSOStateType.TriggeredFixed:
                        SetCabDisplayControl(LS_SF, 1);
                        break;
                }

                if (RSOEmergencyTimer.Triggered)
                {
                    RSOEmergencyBraking = true;
                }
                else if (RearmingButton)
                {
                    RSOEmergencyBraking = false;
                }

                SetCabDisplayControl(VY_SOS_RSO, RSOEmergencyBraking ? 1 : 0);

                if (RSOClosedSignal && !RSOPreviousClosedSignal && !RSOType1Inhibition)
                {
                    TriggerSoundInfo1();
                }

                RSOPreviousClosedSignal = RSOClosedSignal;

                if (TVM300Present || TVM430Present)
                {
                    if (TVMClosedSignal && !TVMPreviousClosedSignal)
                    {
                        TriggerSoundInfo1();
                    }

                    if (TVMOpenedSignal && !TVMPreviousOpenedSignal)
                    {
                        TriggerSoundInfo1();
                    }

                    TVMPreviousClosedSignal = TVMClosedSignal;
                    TVMPreviousOpenedSignal = TVMOpenedSignal;
                }

                RSOPreviousPressed = RSOPressed;
            }
        }

        protected void UpdateKVB()
        {
            if (KVBPresent && IsSpeedControlEnabled())
            {
                if (CurrentPostSpeedLimitMpS() > MpS.FromKpH(221f) && PreviousLineSpeed <= MpS.FromKpH(221f) && SpeedMpS() > 0f)
                {
                    KVBHSLStartOdometer.Start();
                }

                if (KVBHSLStartOdometer.Triggered && KVBMode != KVBModeType.HighSpeedLine)
                {
                    KVBHSLStartOdometer.Stop();
                    KVBSpadEmergency = false;
                    KVBOverspeedEmergency = false;
                    KVBSpeedTooHighLight = false;

                    KVBMode = KVBModeType.HighSpeedLine;
                }
                else if (NextPostSpeedLimitMpS(0) <= MpS.FromKpH(221f) && NextPostDistanceM(0) < 60f && PreviousLineSpeed > MpS.FromKpH(221f) && SpeedMpS() > 0f)
                {
                    KVBKarmEmergency = false;

                    KVBMode = KVBModeType.ConventionalLine;
                }

                switch (KVBMode)
                {
                    case KVBModeType.HighSpeedLine:
                        ResetKVBTargets();

                        KVBKarmEmergency = (!TVM300Present && !TVM430Present) || !TVMArmed;

                        UpdateKVBEmergencyBraking();

                        UpdateKVBDisplay();
                        break;

                    case KVBModeType.ConventionalLine:
                        KVBMode = KVBModeType.ConventionalLine;

                        UpdateKVBParameters();

                        UpdateKVBTargets();

                        UpdateKVBSpeedControl();

                        UpdateKVBEmergencyBraking();

                        UpdateKVBDisplay();

                        // Send data to the simulator
                        if (KVBStopTargetSignalNumber == 0)
                        {
                            SetNextSpeedLimitMpS(0f);
                        }
                        else if (KVBSpeedRestrictionTargetSignalNumber == 0)
                        {
                            SetNextSpeedLimitMpS(KVBSpeedRestrictionTargetSpeedMpS);
                        }
                        else
                        {
                            SetNextSpeedLimitMpS(KVBNextLineSpeedLimitMpS);
                        }
                        SetCurrentSpeedLimitMpS(Math.Min(KVBLastSignalSpeedLimitMpS, KVBCurrentLineSpeedLimitMpS));
                        break;
                }
            }
            else
            {
                KVBEmergencyBraking = false;
            }
        }

        protected void UpdateKVBParameters()
        {
            KVBTrainLengthM = (float)Math.Ceiling((double)(TrainLengthM() / 100f)) * 100f;
            if (ElectroPneumaticBrake)
                KVBDelayBeforeBrakingEstablishedS = 2f;
            else if (HeavyFreightTrain)
                KVBDelayBeforeBrakingEstablishedS = 12f + KVBTrainLengthM / 200f;
            else
                KVBDelayBeforeBrakingEstablishedS = 2f + 2f * KVBTrainLengthM * KVBTrainLengthM * 0.00001f;
        }

        protected void UpdateKVBTargets()
        {
            // Line speed limit
            KVBCurrentLineSpeedLimitMpS = CurrentPostSpeedLimitMpS();
            KVBNextLineSpeedLimitMpS = NextPostSpeedLimitMpS(0) > 0 ? NextPostSpeedLimitMpS(0) : float.PositiveInfinity;
            KVBNextLineSpeedDistanceM = NextPostDistanceM(0);

            // If train is about to cross a normal signal, get its information.
            float nextNormalSignalDistance = NextSignalDistanceM(0);
            if (nextNormalSignalDistance <= 5f)
            {
                KVBLastSignalAspect = NextSignalAspect(0);
                KVBLastSignalSpeedLimitMpS = NextSignalSpeedLimitMpS(0) > 0f ? NextSignalSpeedLimitMpS(0) : float.PositiveInfinity;
            }

            // If train is about to cross a normal signal, get its information.
            float nextDistantSignalDistance = NextDistanceSignalDistanceM();
            if (nextDistantSignalDistance <= 5f)
            {
                KVBLastSignalAspect = NextDistanceSignalAspect();
            }

            // If not on sight, current track node is longer than train length and no switch is in front of us, release the signal speed limit
            float trackNodeOFfset = Locomotive().Train.FrontTDBTraveller.TrackNodeOffset;
            float nextDivergingSwitchDistanceM = NextDivergingSwitchDistanceM(500f);
            float nextTrailingDivergingSwitchDistanceM = NextTrailingDivergingSwitchDistanceM(500f);
            if (!KVBOnSight
                && trackNodeOFfset > KVBTrainLengthM
                && nextDivergingSwitchDistanceM > nextNormalSignalDistance
                && nextTrailingDivergingSwitchDistanceM > nextNormalSignalDistance
                )
            {
                KVBLastSignalSpeedLimitMpS = float.PositiveInfinity;
            }

            if ((NormalSignalPassed || DistantSignalPassed) && SpeedMpS() > 0.1f)
            {
                // Signal passed at danger check
                if (KVBLastSignalAspect == Aspect.Stop)
                {
                    KVBSpadEmergency = true;
                    TriggerSoundPenalty2();
                    Message(ConfirmLevel.Warning, "SOS KVB");
                    Message(ConfirmLevel.Warning, "KVB : Franchissement carré / Signal passed at danger");
                }
                else if (KVBLastSignalAspect == Aspect.StopAndProceed)
                {
                    KVBOnSight = true;
                    KVBLastSignalSpeedLimitMpS = MpS.FromKpH(30);
                }
                // Search for a stop target
                else
                {
                    KVBOnSight = false;

                    int i;
                    Aspect aspect;

                    // Search for the next stop signal
                    for (i = 0; i < 5; i++)
                    {
                        aspect = NextSignalAspect(i);

                        if (aspect == Aspect.Stop
                            || aspect == Aspect.StopAndProceed)
                        {
                            break;
                        }
                    }

                    // If signal found
                    if (i < 5)
                    {
                        KVBStopTargetSignalNumber = i;
                        KVBStopTargetDistanceM = NextSignalDistanceM(i);
                    }
                    else
                    {
                        KVBStopTargetSignalNumber = -1;
                        KVBStopTargetDistanceM = float.PositiveInfinity;
                    }

                    // Reset release speed
                    KVBStopTargetReleaseSpeed = KVBReleaseSpeed.V30;
                }

                // Search for a speed restriction target
                {
                    int i;
                    float speed = 0f;

                    // Search for the next stop signal
                    for (i = 0; i < 5; i++)
                    {
                        speed = NextSignalSpeedLimitMpS(i);

                        if (speed > 0f && speed < KVBTrainSpeedLimitMpS)
                        {
                            break;
                        }
                    }

                    // If signal found
                    if (i < 5)
                    {
                        KVBSpeedRestrictionTargetSignalNumber = i;
                        KVBSpeedRestrictionTargetDistanceM = NextSignalDistanceM(i);
                        KVBSpeedRestrictionTargetSpeedMpS = speed;
                    }
                    else
                    {
                        KVBSpeedRestrictionTargetSignalNumber = -1;
                        KVBSpeedRestrictionTargetDistanceM = float.PositiveInfinity;
                        KVBSpeedRestrictionTargetSpeedMpS = float.PositiveInfinity;
                    }
                }
            }

            // Pre-announce aspect
            switch (KVBPreAnnounce)
            {
                case KVBPreAnnounceType.Deactivated:
                    if (KVBLastSignalSpeedLimitMpS > MpS.FromKpH(160f)
                        && (KVBSpeedRestrictionTargetSignalNumber != 0 || KVBSpeedRestrictionTargetSpeedMpS > MpS.FromKpH(160f))
                        && KVBCurrentLineSpeedLimitMpS > MpS.FromKpH(160f)
                        && (KVBNextLineSpeedLimitMpS > MpS.FromKpH(160f) || KVBNextLineSpeedDistanceM > 3000f))
                    {
                        KVBPreAnnounce = KVBPreAnnounceType.Armed;
                    }
                    break;

                case KVBPreAnnounceType.Armed:
                    if (KVBCurrentLineSpeedLimitMpS <= MpS.FromKpH(160f))
                    {
                        KVBPreAnnounce = KVBPreAnnounceType.Deactivated;
                    }

                    if (NormalSignalPassed
                        && KVBLastSignalSpeedLimitMpS > MpS.FromKpH(160f)
                        && KVBSpeedRestrictionTargetSignalNumber == 0
                        && KVBSpeedRestrictionTargetSpeedMpS <= MpS.FromKpH(160f))
                    {
                        KVBPreAnnounce = KVBPreAnnounceType.Triggered;
                        TriggerSoundInfo2();
                    }
                    // TODO : Use the P sign in order to locate the point where pre-announce must be deactivated (instead of 3000m before the start of the restriction)
                    else if (KVBNextLineSpeedLimitMpS <= MpS.FromKpH(160f)
                        && KVBNextLineSpeedDistanceM <= 3000f)
                    {
                        KVBPreAnnounce = KVBPreAnnounceType.Triggered;
                        TriggerSoundInfo2();
                    }
                    break;

                case KVBPreAnnounceType.Triggered:
                    if (KVBCurrentLineSpeedLimitMpS <= MpS.FromKpH(160f)
                        || KVBLastSignalSpeedLimitMpS <= MpS.FromKpH(160f))
                    {
                        KVBPreAnnounce = KVBPreAnnounceType.Deactivated;
                    }
                    break;
            }

            // Update distances
            if (KVBStopTargetSignalNumber >= 0)
            {
                KVBStopTargetDistanceM = NextSignalDistanceM(KVBStopTargetSignalNumber);

                // Proximity to a C aspect
                if (KVBStopTargetDistanceM <= 200f
                    && KVBStopTargetReleaseSpeed == KVBReleaseSpeed.V30
                    && NextSignalAspect(KVBStopTargetSignalNumber) == Aspect.Stop)
                {
                    KVBStopTargetReleaseSpeed = KVBReleaseSpeed.V10;
                }
            }

            if (KVBSpeedRestrictionTargetSignalNumber >= 0)
            {
                KVBSpeedRestrictionTargetDistanceM = NextSignalDistanceM(KVBSpeedRestrictionTargetSignalNumber);
            }
        }

        protected void UpdateKVBSpeedControl()
        {
            float KVBStopTargetAlertSpeedMpS = MpS.FromKpH(5f);
            float KVBStopTargetEBSpeedMpS = MpS.FromKpH(10f);
            float KVBStopTargetReleaseSpeedMpS = MpS.FromKpH(30f);
            if (KVBStopTargetReleaseSpeed == KVBReleaseSpeed.V10)
            {
                KVBStopTargetAlertSpeedMpS = MpS.FromKpH(2.5f);
                KVBStopTargetEBSpeedMpS = MpS.FromKpH(5f);
                KVBStopTargetReleaseSpeedMpS = MpS.FromKpH(10f);
            }

            bool alert = false;
            bool emergency = false;
            KVBSpeedTooHighLight = false;

            // Train speed limit
            alert |= SpeedMpS() > KVBTrainSpeedLimitMpS + MpS.FromKpH(5f);
            emergency |= SpeedMpS() > KVBTrainSpeedLimitMpS + MpS.FromKpH(10f);

            // Stop aspect
            if (KVBStopTargetSignalNumber >= 0)
            {
                alert |= CheckKVBSpeedCurve(
                    KVBStopTargetDistanceM,
                    0f,
                    KVBDeclivity,
                    KVBDelayBeforeBrakingEstablishedS + KVBDelayBeforeEmergencyBrakingS,
                    KVBStopTargetAlertSpeedMpS,
                    KVBStopTargetReleaseSpeedMpS);

                emergency |= CheckKVBSpeedCurve(
                    KVBStopTargetDistanceM,
                    0f,
                    KVBDeclivity,
                    KVBDelayBeforeBrakingEstablishedS,
                    KVBStopTargetEBSpeedMpS,
                    KVBStopTargetReleaseSpeedMpS);
            }

            // Speed restriction
            if (KVBSpeedRestrictionTargetSignalNumber >= 0)
            {
                alert |= CheckKVBSpeedCurve(
                    KVBSpeedRestrictionTargetDistanceM,
                    KVBSpeedRestrictionTargetSpeedMpS,
                    KVBDeclivity,
                    KVBDelayBeforeBrakingEstablishedS + KVBDelayBeforeEmergencyBrakingS,
                    MpS.FromKpH(5f),
                    KVBSpeedRestrictionTargetSpeedMpS);

                emergency |= CheckKVBSpeedCurve(
                    KVBSpeedRestrictionTargetDistanceM,
                    KVBSpeedRestrictionTargetSpeedMpS,
                    KVBDeclivity,
                    KVBDelayBeforeBrakingEstablishedS,
                    MpS.FromKpH(10f),
                    KVBSpeedRestrictionTargetSpeedMpS);
            }

            // Current speed restriction
            alert |= SpeedMpS() > KVBLastSignalSpeedLimitMpS + MpS.FromKpH(5f);
            KVBSpeedTooHighLight |= SpeedMpS() > KVBLastSignalSpeedLimitMpS + MpS.FromKpH(5f);
            emergency |= SpeedMpS() > KVBLastSignalSpeedLimitMpS + MpS.FromKpH(10f);

            // Current line speed
            if (KVBCurrentLineSpeedLimitMpS > MpS.FromKpH(160f) && KVBPreAnnounce == KVBPreAnnounceType.Deactivated)
            {
                alert |= SpeedMpS() > MpS.FromKpH(160f) + MpS.FromKpH(5f);
                KVBSpeedTooHighLight |= SpeedMpS() > MpS.FromKpH(160f) + MpS.FromKpH(5f);
                emergency |= SpeedMpS() > MpS.FromKpH(160f) + MpS.FromKpH(10f);
            }
            else
            {
                alert |= SpeedMpS() > KVBCurrentLineSpeedLimitMpS + MpS.FromKpH(5f);
                KVBSpeedTooHighLight |= SpeedMpS() > KVBCurrentLineSpeedLimitMpS + MpS.FromKpH(5f);
                emergency |= SpeedMpS() > KVBCurrentLineSpeedLimitMpS + MpS.FromKpH(10f);
            }

            // Next line speed
            if (KVBNextLineSpeedLimitMpS < KVBCurrentLineSpeedLimitMpS)
            {
                alert |= CheckKVBSpeedCurve(
                    KVBNextLineSpeedDistanceM,
                    KVBNextLineSpeedLimitMpS,
                    KVBDeclivity,
                    KVBDelayBeforeBrakingEstablishedS + KVBDelayBeforeEmergencyBrakingS,
                    MpS.FromKpH(5f),
                    KVBNextLineSpeedLimitMpS);

                emergency |= CheckKVBSpeedCurve(
                    KVBNextLineSpeedDistanceM,
                    KVBNextLineSpeedLimitMpS,
                    KVBDeclivity,
                    KVBDelayBeforeBrakingEstablishedS,
                    MpS.FromKpH(10f),
                    KVBNextLineSpeedLimitMpS);
            }

            switch (KVBState)
            {
                case KVBStateType.Normal:
                    if (alert)
                    {
                        TriggerSoundPenalty1();
                        KVBState = KVBStateType.Alert;
                        Message(ConfirmLevel.Warning, "KVB : Survitesse / Overspeed");
                    }
                    break;

                case KVBStateType.Alert:
                    if (!alert)
                    {
                        KVBState = KVBStateType.Normal;
                    }
                    else if (emergency)
                    {
                        TriggerSoundPenalty2();
                        KVBState = KVBStateType.Emergency;
                        Message(ConfirmLevel.Warning, "KVB : Survitesse / Overspeed");
                        Message(ConfirmLevel.Warning, "SOS KVB");
                    }
                    break;

                case KVBStateType.Emergency:
                    if (SpeedMpS() < 0.1f)
                    {
                        KVBState = KVBStateType.Normal;
                    }
                    break;
            }

            KVBOverspeedEmergency = KVBState == KVBStateType.Emergency;
        }

        protected void UpdateKVBEmergencyBraking()
        {
            if (KVBSpadEmergency && SpeedMpS() < 0.1f)
            {
                KVBSpadEmergency = false;
            }

            if (KVBOverspeedEmergency && SpeedMpS() < 0.1f)
            {
                KVBOverspeedEmergency = false;
            }

            if (KVBKarmEmergency && TVMArmed)
            {
                KVBKarmEmergency = false;
            }

            if (!KVBEmergencyBraking)
            {
                if (KVBSpadEmergency || KVBOverspeedEmergency || KVBKarmEmergency)
                {
                    KVBEmergencyBraking = true;
                }
            }
            else
            {
                if (!KVBSpadEmergency && !KVBOverspeedEmergency && !KVBKarmEmergency && RearmingButton)
                {
                    KVBEmergencyBraking = false;

                    // On sight till the end of the block section
                    KVBOnSight = true;
                    KVBLastSignalSpeedLimitMpS = MpS.FromKpH(30);
                }
            }
        }

        protected void UpdateKVBDisplay()
        {
            SetOverspeedWarningDisplay(KVBState >= KVBStateType.Alert);

            if (KVBMode != KVBModeType.HighSpeedLine)
            {
                if (KVBPreAnnounce == KVBPreAnnounceType.Armed)
                {
                    SetNextSignalAspect(Aspect.Clear_2);
                }
                else if (KVBStopTargetReleaseSpeed == KVBReleaseSpeed.V10)
                {
                    SetNextSignalAspect(Aspect.Stop);
                }
                else
                {
                    SetNextSignalAspect(Aspect.Clear_1);
                }
            }

            // VY SOS KVB
            SetCabDisplayControl(VY_SOS_KVB, KVBEmergencyBraking ? 1 : 0);

            // VY VTE
            SetCabDisplayControl(VY_VTE, KVBSpeedTooHighLight ? 1 : 0);

            // VY FU
            KVBEmergencyBrakeLight = KVBSpadEmergency || KVBOverspeedEmergency || KVBKarmEmergency;
            SetCabDisplayControl(VY_FU, KVBEmergencyBrakeLight ? 1 : 0);
        }

        protected bool CheckKVBSpeedCurve(float targetDistanceM, float targetSpeedMpS, float slope, float delayS, float marginMpS, float releaseSpeedMpS)
        {
            float speedCurveMpS =
                Math.Max(
                    SpeedCurve(
                        targetDistanceM,
                        targetSpeedMpS,
                        slope,
                        delayS,
                        SafeDecelerationMpS2
                    ),
                    releaseSpeedMpS + marginMpS
                );

            return SpeedMpS() > speedCurveMpS;
        }

        protected void ResetKVBTargets()
        {
            KVBPreAnnounce = KVBPreAnnounceType.Deactivated;

            KVBLastSignalAspect = Aspect.Clear_1;
            KVBLastSignalSpeedLimitMpS = float.PositiveInfinity;

            KVBStopTargetSignalNumber = -1;
            KVBStopTargetDistanceM = float.PositiveInfinity;
            KVBStopTargetReleaseSpeed = KVBReleaseSpeed.V30;
            KVBOnSight = false;

            KVBSpeedRestrictionTargetSignalNumber = -1;
            KVBSpeedRestrictionTargetDistanceM = float.PositiveInfinity;
            KVBSpeedRestrictionTargetSpeedMpS = float.PositiveInfinity;

            KVBCurrentLineSpeedLimitMpS = float.PositiveInfinity;
            KVBNextLineSpeedLimitMpS = float.PositiveInfinity;
            KVBNextLineSpeedDistanceM = float.PositiveInfinity;

            KVBState = KVBStateType.Normal;
        }

        protected void UpdateTVM()
        {
            if ((TVM300Present || TVM430Present) && IsSpeedControlEnabled())
            {
                // Automatic arming
                if (NextPostSpeedLimitMpS(0) > MpS.FromKpH(221f) && NextPostDistanceM(0) < 5f && PreviousLineSpeed <= MpS.FromKpH(221f) && SpeedMpS() > 0f && !TVMArmed)
                {
                    TVMArmed = true;
                    UpdateTVMAspect(NextSignalAspect(0), false);
                }

                // Automatic dearming
                if (CurrentPostSpeedLimitMpS() <= MpS.FromKpH(221f) && PreviousLineSpeed > MpS.FromKpH(221f) && SpeedMpS() > 0f && TVMArmed)
                {
                    TVMArmed = false;
                    TVMCOVITEmergencyBraking = false;
                }

                if (TVMArmed)
                {
                    // TVM mask
                    SetCabDisplayControl(TVM_Mask, 1);

                    if (TVM300Present)
                    {
                        UpdateTVM300Display();
                        UpdateTVM300COVIT();
                    }
                    else if (TVM430Present)
                    {
                        UpdateTVM430Display();
                        UpdateTVM430COVIT();
                    }
                }
                else
                {
                    // TVM mask
                    SetCabDisplayControl(TVM_Mask, 0);

                    TVMAspect = Aspect.None;
                    TVMPreviousAspect = Aspect.None;
                }
            }
            else
            {
                TVMArmed = false;
                TVMCOVITEmergencyBraking = false;
            }
        }

        protected void UpdateTVM300Display()
        {
            UpdateTVMAspect(NextSignalAspect(0));
        }

        protected void UpdateTVM430Display()
        {
            if (NextSignalAspect(0) != TVMAspect)
            {
                if (!TVM430AspectChangeTimer.Started)
                {
                    TVM430AspectChangeTimer.Start();
                }
                else
                {
                    if (TVM430AspectChangeTimer.Triggered)
                    {
                        UpdateTVMAspect(NextSignalAspect(0));

                        TVM430AspectChangeTimer.Stop();
                    }
                }
            }
            else
            {
                UpdateTVMAspect(NextSignalAspect(0));
            }
        }

        protected void UpdateTVMAspect(Aspect aspect, bool updateRSO = true)
        {
            TVMPreviousAspect = TVMAspect;
            TVMAspect = aspect;
            SetNextSignalAspect(aspect);

            if (updateRSO && TVMAspect != Aspect.None && TVMPreviousAspect != Aspect.None)
            {
                TVMClosedSignal = (TVMPreviousAspect < TVMAspect);
                TVMOpenedSignal = (TVMPreviousAspect > TVMAspect);
            }
        }

        protected void UpdateTVM300COVIT()
        {
            if (TVMCOVITInhibited)
            {
                TVMCOVITEmergencyBraking = false;
            }
            else
            {
                TVM300CurrentSpeedLimitMpS = MpS.FromKpH(TVM300CurrentSpeedLimitsKph[NextSignalAspect(0)]);
                TVM300NextSpeedLimitMpS = MpS.FromKpH(TVM300NextSpeedLimitsKph[NextSignalAspect(0)]);

                SetNextSpeedLimitMpS(TVM300NextSpeedLimitMpS);
                SetCurrentSpeedLimitMpS(TVM300CurrentSpeedLimitMpS);

                TVM300EmergencySpeedMpS = TVM300GetEmergencySpeed(TVM300CurrentSpeedLimitMpS);

                if (!TVMCOVITEmergencyBraking && SpeedMpS() > TVM300CurrentSpeedLimitMpS + TVM300EmergencySpeedMpS)
                    TVMCOVITEmergencyBraking = true;

                if (TVMCOVITEmergencyBraking && SpeedMpS() <= TVM300CurrentSpeedLimitMpS)
                    TVMCOVITEmergencyBraking = false;
            }
        }

        protected void UpdateTVM430COVIT()
        {
            if (TVMCOVITInhibited)
            {
                TVMCOVITEmergencyBraking = false;
            }
            else
            {
                if (TVM430TrainSpeedLimitMpS == MpS.FromKpH(320f))
                {
                    TVM430CurrentSpeedLimitMpS = MpS.FromKpH(TVM430S320CurrentSpeedLimitsKph[NextSignalAspect(0)]);
                    TVM430NextSpeedLimitMpS = MpS.FromKpH(TVM430S320NextSpeedLimitsKph[NextSignalAspect(0)]);
                }
                else
                {
                    TVM430CurrentSpeedLimitMpS = MpS.FromKpH(TVM430S300CurrentSpeedLimitsKph[NextSignalAspect(0)]);
                    TVM430NextSpeedLimitMpS = MpS.FromKpH(TVM430S300NextSpeedLimitsKph[NextSignalAspect(0)]);
                }

                SetNextSpeedLimitMpS(TVM430NextSpeedLimitMpS);
                SetCurrentSpeedLimitMpS(TVM430CurrentSpeedLimitMpS);

                TVM430CurrentEmergencySpeedMpS = TVM430GetEmergencySpeed(TVM430CurrentSpeedLimitMpS);
                TVM430NextEmergencySpeedMpS = TVM430GetEmergencySpeed(TVM430NextSpeedLimitMpS);

                if (NormalSignalPassed)
                {
                    TVM430EmergencyDecelerationMpS2 = Deceleration(
                        TVM430CurrentSpeedLimitMpS + TVM430CurrentEmergencySpeedMpS,
                        TVM430NextSpeedLimitMpS + TVM430NextEmergencySpeedMpS,
                        NextSignalDistanceM(0)
                    );

                    TVM430ResetDecelerationMpS2 = Deceleration(
                        TVM430CurrentSpeedLimitMpS,
                        TVM430NextSpeedLimitMpS,
                        NextSignalDistanceM(0)
                    );
                }

                TVM430EmergencySpeedCurveMpS = SpeedCurve(
                    NextSignalDistanceM(0),
                    TVM430NextSpeedLimitMpS + TVM430NextEmergencySpeedMpS,
                    0,
                    0,
                    TVM430EmergencyDecelerationMpS2
                );

                TVM430ResetSpeedCurveMpS = SpeedCurve(
                    NextSignalDistanceM(0),
                    TVM430NextSpeedLimitMpS,
                    0,
                    0,
                    TVM430ResetDecelerationMpS2
                );

                if (!TVMCOVITEmergencyBraking && SpeedMpS() > TVM430EmergencySpeedCurveMpS)
                    TVMCOVITEmergencyBraking = true;

                if (TVMCOVITEmergencyBraking && SpeedMpS() <= TVM430ResetSpeedCurveMpS)
                    TVMCOVITEmergencyBraking = false;
            }
        }

        private float TVM300GetEmergencySpeed(float speedLimit)
        {
            float emergencySpeed = 0f;

            if (speedLimit <= MpS.FromKpH(80f))
                emergencySpeed = MpS.FromKpH(5f);
            else if (speedLimit <= MpS.FromKpH(160f))
                emergencySpeed = MpS.FromKpH(10f);
            else
                emergencySpeed = MpS.FromKpH(15f);

            return emergencySpeed;
        }

        private float TVM430GetEmergencySpeed(float speedLimit)
        {
            float emergencySpeed = 0f;

            if (speedLimit <= MpS.FromKpH(80f))
                emergencySpeed = MpS.FromKpH(5f);
            else if (speedLimit <= MpS.FromKpH(170f))
                emergencySpeed = MpS.FromKpH(10f);
            else if (speedLimit <= MpS.FromKpH(270f))
                emergencySpeed = MpS.FromKpH(15f);
            else
                emergencySpeed = MpS.FromKpH(20f);

            return emergencySpeed;
        }

        public override void HandleEvent(TCSEvent evt, string message)
        {
            switch (evt)
            {
                case TCSEvent.AlerterPressed:
                    VACMAPressed = true;
                    break;

                case TCSEvent.AlerterReleased:
                    VACMAPressed = false;
                    break;

                case TCSEvent.ThrottleChanged:
                case TCSEvent.DynamicBrakeChanged:
                case TCSEvent.HornActivated:
                    if (VACMAPressedAlertTimer.Started || VACMAPressedEmergencyTimer.Started)
                    {
                        VACMAPressedAlertTimer.Start();
                        VACMAPressedEmergencyTimer.Start();
                    }
                    break;

                case TCSEvent.GenericTCSButtonPressed:
                    {
                        int tcsButton = -1;
                        if (Int32.TryParse(message, out tcsButton))
                        {
                            SetCabDisplayControl(tcsButton, 1);

                            switch (tcsButton)
                            {
                                // BP (AC) SF
                                case BP_AC_SF:
                                    RSOPressed = true;
                                    break;

                                // BP (A) LS (SF)
                                case BP_A_LS_SF:
                                    RSOCancelPressed = true;
                                    break;
                            }
                        }
                    }
                    break;

                case TCSEvent.GenericTCSButtonReleased:
                    {
                        int tcsButton = -1;
                        if (Int32.TryParse(message, out tcsButton))
                        {
                            SetCabDisplayControl(tcsButton, 0);

                            switch (tcsButton)
                            {
                                // BP (AC) SF
                                case BP_AC_SF:
                                    RSOPressed = false;
                                    break;

                                // BP (A) LS (SF)
                                case BP_A_LS_SF:
                                    RSOCancelPressed = false;
                                    break;

                                // BP AM V1 and BP AM V2
                                case BP_AM_V1:
                                case BP_AM_V2:
                                    if (!TVMArmed)
                                    {
                                        TVMArmed = true;
                                        UpdateTVMAspect(NextSignalAspect(0), false);
                                    }
                                    break;

                                // BP DM
                                case BP_DM:
                                    TVMArmed = false;
                                    break;
                            }
                        }
                    }
                    break;

                case TCSEvent.GenericTCSSwitchOn:
                    {
                        int tcsButton = -1;
                        if (Int32.TryParse(message, out tcsButton))
                        {
                            SetCabDisplayControl(tcsButton, 1);

                            switch (tcsButton)
                            {
                                // Z (ES) VA
                                case Z_ES_VA:
                                    VACMATest = true;
                                    break;
                            }
                        }
                    }
                    break;

                case TCSEvent.GenericTCSSwitchOff:
                    {
                        int tcsButton = -1;
                        if (Int32.TryParse(message, out tcsButton))
                        {
                            SetCabDisplayControl(tcsButton, 0);

                            switch (tcsButton)
                            {
                                // Z (ES) VA
                                case Z_ES_VA:
                                    VACMATest = false;
                                    break;
                            }
                        }
                    }
                    break;
            }
        }

        protected void UpdateVACMA()
        {
            if (VACMAPresent && Activated && IsAlerterEnabled())
            {
                if (SpeedMpS() >= VACMAActivationSpeedMpS || VACMATest)
                {
                    if (VACMAPressed && (!VACMAPressedAlertTimer.Started || !VACMAPressedEmergencyTimer.Started))
                    {
                        VACMAReleasedAlertTimer.Stop();
                        VACMAReleasedEmergencyTimer.Stop();
                        VACMAPressedAlertTimer.Start();
                        VACMAPressedEmergencyTimer.Start();
                    }
                    if (!VACMAPressed && (!VACMAReleasedAlertTimer.Started || !VACMAReleasedEmergencyTimer.Started))
                    {
                        VACMAReleasedAlertTimer.Start();
                        VACMAReleasedEmergencyTimer.Start();
                        VACMAPressedAlertTimer.Stop();
                        VACMAPressedEmergencyTimer.Stop();
                    }
                }
                else
                {
                    VACMAReleasedAlertTimer.Stop();
                    VACMAReleasedEmergencyTimer.Stop();
                    VACMAPressedAlertTimer.Stop();
                    VACMAPressedEmergencyTimer.Stop();
                }

                if (VACMAReleasedAlertTimer.Started && VACMAReleasedAlertTimer.Triggered)
                    TriggerSoundWarning1();
                else
                    TriggerSoundWarning2();

                if (VACMAPressedAlertTimer.Started && VACMAPressedAlertTimer.Triggered)
                    TriggerSoundAlert1();
                else
                    TriggerSoundAlert2();

                if (!VACMAEmergencyBraking && (VACMAPressedEmergencyTimer.Triggered || VACMAReleasedEmergencyTimer.Triggered))
                {
                    VACMAEmergencyBraking = true;
                    SetVigilanceEmergencyDisplay(true);
                }

                if (VACMAEmergencyBraking && SpeedMpS() < VACMAActivationSpeedMpS && RearmingButton)
                {
                    VACMAEmergencyBraking = false;
                    SetVigilanceEmergencyDisplay(false);
                }
            }
            else
            {
                // Reset everything
                VACMAReleasedAlertTimer.Stop();
                VACMAReleasedEmergencyTimer.Stop();
                VACMAPressedAlertTimer.Stop();
                VACMAPressedEmergencyTimer.Stop();
                VACMAEmergencyBraking = false;
                SetVigilanceEmergencyDisplay(false);

                TriggerSoundWarning2();
                TriggerSoundAlert2();
                return;
            }

            // VY SOS VAC
            SetCabDisplayControl(VY_SOS_VAC, VACMAEmergencyBraking ? 1 : 0);

            // VY (ES) FU
            SetCabDisplayControl(VY_ES_FU, VACMATest && IsBrakeEmergency() && !TractionAuthorization() ? 1 : 0);
        }

        protected void UpdateSignalPassed()
        {
            NormalSignalPassed = NextSignalDistanceM(0) > PreviousNormalSignalDistanceM;

            PreviousNormalSignalDistanceM = NextSignalDistanceM(0);

            DistantSignalPassed = NextDistanceSignalDistanceM() > PreviousDistantSignalDistanceM;

            PreviousDistantSignalDistanceM = NextDistanceSignalDistanceM();
        }
    }
}