// COPYRIGHT 2014 by the Open Rails project.
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
using ORTS.Common;
using ORTS.Scripting.Api;
using System;
using System.Collections.Generic;

namespace ORTS.Scripting.Script
{
    public class TCS_France : TrainControlSystem
    {
        enum CCS
        {
            RSO,        // RSO only
            DAAT,       // RSO + DAAT
            KVB,        // RSO + KVB
            TVM300,     // RSO partially inhibited + KVB partially inhibited + TVM300
            TVM430,     // RSO partially inhibited + KVB partially inhibited + TVM430
            ETCS        // ETCS only
        }

        enum ETCSLevel
        {
            L0,         // Unfitted (national system active)
            NTC,        // Specific Transmission Module (national system information transmitted to ETCS)
            L1,         // Level 1 : Beacon transmission, loop transmission and radio in-fill
            L2,         // Level 2 : Radio transmission, beacon positionning
            L3          // Level 3 : Same as level 2 + moving block
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

        CCS ActiveCCS = CCS.RSO;
        CCS PreviousCCS = CCS.RSO;
        ETCSLevel CurrentETCSLevel = ETCSLevel.L0;

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
        const float RSODelayBeforeEmergencyBrakingS = 4f;

        // Variables
        bool RSOEmergencyBraking = false;
        bool RSOType1Inhibition = false;                    // Inhibition 1 : Reverse
        bool RSOType2Inhibition = false;                    // Inhibition 2 : KVB not inhibited and train on HSL
        bool RSOType3Inhibition = false;                    // Inhibition 3 : TVM COVIT not inhibited
        bool RSOClosedSignal = false;
        bool RSOPreviousClosedSignal = false;
        bool RSOOpenedSignal = false;
        bool RSOPreviousOpenedSignal = false;

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
        float InitCount = 0;

        KVBStateType KVBState = KVBStateType.Normal;
        KVBPreAnnounceType KVBPreAnnounce = KVBPreAnnounceType.Deactivated;
        KVBModeType KVBMode = KVBModeType.ConventionalLine;

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
        bool VACMAEmergencyBraking = false;
        bool VACMAPressed = false;
        Timer VACMAPressedAlertTimer;
        Timer VACMAPressedEmergencyTimer;
        Timer VACMAReleasedAlertTimer;
        Timer VACMAReleasedEmergencyTimer;

        // Other variables
        bool ExternalEmergencyBraking = false;

        float PreviousNormalSignalDistanceM = 0f;
        bool NormalSignalPassed = false;

        float PreviousDistantSignalDistanceM = 0f;
        bool DistantSignalPassed = false;

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

            // KVB section
            KVBInhibited = GetBoolParameter("KVB", "Inhibited", false);
            KVBTrainSpeedLimitMpS = MpS.FromKpH(GetFloatParameter("KVB", "TrainSpeedLimitKpH", 160f));

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
            Activated = true;

            SetNextSignalAspect(Aspect.Clear_1);
        }

        public override void Update()
        {
            if (InitCount < 5)
            {
                InitCount++;
                return;
            }

            UpdateSignalPassed();

            UpdateVACMA();

            if (IsTrainControlEnabled() && IsSpeedControlEnabled())
            {
                if (RSOPresent)
                {
                    UpdateRSO();
                }

                if (CurrentPostSpeedLimitMpS() <= MpS.FromKpH(220f))
                {
                    if (KVBPresent)
                    {
                        // Classic line = KVB active
                        ActiveCCS = CCS.KVB;

                        UpdateKVB();
                    }
                    else
                    {
                        if (!DAATPresent)
                        {
                            ActiveCCS = CCS.RSO;
                        }
                        else
                        {
                            ActiveCCS = CCS.DAAT;
                        }

                        SetNextSignalAspect(Aspect.Clear_1);
                    }
                }
                else
                {
                    // High speed line = TVM active

                    // Activation control (KAr) in KVB system
                    if (TVM300Present)
                    {
                        ActiveCCS = CCS.TVM300;

                        UpdateTVM300Display();
                        UpdateTVM300COVIT();
                    }
                    else if (TVM430Present)
                    {
                        ActiveCCS = CCS.TVM430;

                        UpdateTVM430Display();
                        UpdateTVM430COVIT();
                    }
                }

                SetEmergencyBrake(
                    RSOEmergencyBraking
                    || KVBState == KVBStateType.Emergency
                    || TVMCOVITEmergencyBraking
                    || VACMAEmergencyBraking
                    || ExternalEmergencyBraking
                );

                SetPenaltyApplicationDisplay(IsBrakeEmergency());

                SetPowerAuthorization(!RSOEmergencyBraking
                    && KVBState != KVBStateType.Emergency
                    && !TVMCOVITEmergencyBraking
                    && !VACMAEmergencyBraking
                );

                if (ActiveCCS != CCS.TVM300 && ActiveCCS != CCS.TVM430)
                {
                    TVMAspect = Aspect.None;
                    TVMPreviousAspect = Aspect.None;
                }

                RSOType1Inhibition = IsDirectionReverse();
                RSOType2Inhibition = !KVBInhibited && ((TVM300Present && ActiveCCS == CCS.TVM300) || (TVM430Present && ActiveCCS == CCS.TVM430));
                RSOType3Inhibition = (TVM300Present || TVM430Present) && !TVMCOVITInhibited;

                PreviousCCS = ActiveCCS;
            }
        }

        public override void SetEmergency(bool emergency)
        {
            ExternalEmergencyBraking = emergency;
        }

        protected void UpdateRSO()
        {
            if (NextSignalDistanceM(0) < 2f
                && (ActiveCCS == CCS.RSO || ActiveCCS == CCS.DAAT || ActiveCCS == CCS.KVB)
                && SpeedMpS() > 0)
            {
                if (NextSignalAspect(0) == Aspect.Stop
                    || NextSignalAspect(0) == Aspect.StopAndProceed
                    || NextSignalAspect(0) == Aspect.Restricted
                    || NextSignalAspect(0) == Aspect.Approach_1
                    || NextSignalAspect(0) == Aspect.Approach_2
                    || NextSignalAspect(0) == Aspect.Approach_3
                    )
                    RSOClosedSignal = true;
                else if (NextSignalSpeedLimitMpS(1) > 0f && NextSignalSpeedLimitMpS(1) < MpS.FromKpH(160f))
                    RSOClosedSignal = true;
                else
                    RSOOpenedSignal = true;
            }
            if (NormalSignalPassed || DistantSignalPassed)
                RSOClosedSignal = RSOOpenedSignal = false;

            if (RSOClosedSignal && !RSOPreviousClosedSignal && !RSOType1Inhibition)
                TriggerSoundInfo1();

            RSOPreviousClosedSignal = RSOClosedSignal;

            if ((TVM300Present || TVM430Present) && TVMClosedSignal && !TVMPreviousClosedSignal)
                TriggerSoundInfo1();

            if ((TVM300Present || TVM430Present) && TVMOpenedSignal && !TVMPreviousOpenedSignal)
                TriggerSoundInfo1();

            TVMPreviousClosedSignal = TVMClosedSignal;
            TVMPreviousOpenedSignal = TVMOpenedSignal;
        }

        protected void UpdateKVB()
        {
            if (CurrentPostSpeedLimitMpS() > MpS.FromKpH(220f))
            {
                KVBMode = KVBModeType.HighSpeedLine;

                ResetKVBTargets();

                if (!TVM300Present && !TVM430Present)
                {
                    KVBState = KVBStateType.Emergency;
                }
            }
            else
            {
                KVBMode = KVBModeType.ConventionalLine;

                UpdateKVBParameters();

                UpdateKVBTargets();

                UpdateKVBSpeedControl();

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

            if (NormalSignalPassed || DistantSignalPassed)
            {
                // Signal passed at danger check
                if (KVBLastSignalAspect == Aspect.Stop)
                {
                    KVBState = KVBStateType.Emergency;
                    TriggerSoundPenalty2();

                    // On sight till the end of the block section
                    KVBOnSight = true;
                    KVBLastSignalSpeedLimitMpS = MpS.FromKpH(30);
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
            emergency |= SpeedMpS() > KVBLastSignalSpeedLimitMpS + MpS.FromKpH(10f);

            // Current line speed
            if (KVBCurrentLineSpeedLimitMpS > MpS.FromKpH(160f) && KVBPreAnnounce == KVBPreAnnounceType.Deactivated)
            {
                alert |= SpeedMpS() > MpS.FromKpH(160f) + MpS.FromKpH(5f);
                emergency |= SpeedMpS() > MpS.FromKpH(160f) + MpS.FromKpH(10f);
            }
            else
            {
                alert |= SpeedMpS() > KVBCurrentLineSpeedLimitMpS + MpS.FromKpH(5f);
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
                    }
                    break;

                case KVBStateType.Emergency:
                    if (SpeedMpS() < 0.1f)
                    {
                        KVBState = KVBStateType.Normal;
                    }
                    break;
            }
        }

        protected void UpdateKVBDisplay()
        {
            SetOverspeedWarningDisplay(KVBState >= KVBStateType.Alert);

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

        protected void UpdateTVMAspect(Aspect aspect)
        {
            TVMPreviousAspect = TVMAspect;
            TVMAspect = aspect;
            SetNextSignalAspect(aspect);

            if (TVMAspect != Aspect.None && TVMPreviousAspect != Aspect.None)
            {
                TVMClosedSignal = (TVMPreviousAspect < TVMAspect);
                TVMOpenedSignal = (TVMPreviousAspect > TVMAspect);
            }
        }

        protected void UpdateTVM300COVIT()
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

        protected void UpdateTVM430COVIT()
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
            }
        }

        protected void UpdateVACMA()
        {
            if (!VACMAPresent || !Activated || !IsTrainControlEnabled() || !IsAlerterEnabled() || SpeedMpS() < VACMAActivationSpeedMpS)
            {
                // Reset everything
                VACMAReleasedAlertTimer.Stop();
                VACMAReleasedEmergencyTimer.Stop();
                VACMAPressedAlertTimer.Stop();
                VACMAPressedEmergencyTimer.Stop();
                VACMAEmergencyBraking = false;

                TriggerSoundWarning2();
                TriggerSoundAlert2();
                return;
            }

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

            if (VACMAEmergencyBraking && SpeedMpS() < 0.1f)
            {
                VACMAEmergencyBraking = false;
                SetVigilanceEmergencyDisplay(false);
            }
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