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

using System;
using ORTS.Common;
using ORTS.Scripting.Api;

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

        CCS ActiveCCS = CCS.RSO;
        CCS PreviousCCS = CCS.RSO;
        ETCSLevel CurrentETCSLevel = ETCSLevel.L0;

        // Train parameters
        bool DAATPresent = false;                           // DAAT
        bool KVBPresent = true;                             // KVB
        bool TVM300Present = true;                          // TVM300
        bool TVM430Present = false;                         // TVM430 (Not implemented)
        bool ETCSPresent = false;                           // ETCS (Not implemented)
        ETCSLevel ETCSMaxLevel = ETCSLevel.L0;              // ETCS maximum level (Not implemented)
        bool ElectroPneumaticBrake = true;                  // EP
        bool HeavyFreightTrain = false;                     // MA train only
        float TrainLengthM = 400f;                          // L
        float MaxSpeedLimitMpS = MpS.FromKpH(320);          // VT
        float BrakingEstablishedDelayS = 2f;                // Tbo
        float DecelerationMpS2 = 0.9f;                      // Gamma

        // RSO (Répétition Optique des Signaux / Optical Signal Repetition)
        float RSOEmergencyBrakeDelay = 4f;
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
        bool KVBInhibited = false;
        float KVBEmergencyBrakingAnticipationTimeS = 5f;    // Tx
        float KVBTrainSpeedLimitMpS = MpS.FromKpH(220f);     // VT

        bool KVBPreviousOverspeed = false;
        bool KVBEmergencyBraking = false;
        bool KVBPreviousEmergencyBraking = false;
        bool KVBPreAnnounceActive = false;
        bool KVBPreviousPreAnnounceActive = false;

        float KVBCurrentSignalSpeedLimitMpS;
        float KVBNextSignalSpeedLimitMpS;
        float KVBSignalTargetSpeedMpS;
        float KVBSignalTargetDistanceM;
        float KVBDeclivity = 0f;                            // i

        float KVBCurrentSpeedPostSpeedLimitMpS;
        float KVBNextSpeedPostSpeedLimitMpS;
        float KVBSpeedPostTargetSpeedMpS;
        float KVBSpeedPostTargetDistanceM;

        float KVBCurrentAlertSpeedMpS;
        float KVBCurrentEBSpeedMpS;
        float KVBNextAlertSpeedMpS;
        float KVBNextEBSpeedMpS;

        float KVBSignalEmergencySpeedCurveMpS;
        float KVBSignalAlertSpeedCurveMpS;
        float KVBSpeedPostEmergencySpeedCurveMpS;
        float KVBSpeedPostAlertSpeedCurveMpS;

        // TVM COVIT common
        bool TVMCOVITInhibited = false;

        Aspect TVMPreviousAspect;
        bool TVMClosedSignal;
        bool TVMPreviousClosedSignal;
        bool TVMOpenedSignal;
        bool TVMPreviousOpenedSignal;

        // TVM300 COVIT (Transmission Voie Machine 300 COntrôle de VITesse / Track Machine Transmission 300 Speed control)
        float TVM300TrainSpeedLimitMpS = MpS.FromKpH(300f);

        float TVM300CurrentSpeedLimitMpS;
        float TVM300NextSpeedLimitMpS;
        float TVM300EmergencySpeedMpS;
        bool TVM300EmergencyBraking;

        // TVM430 COVIT (Transmission Voie Machine 430 COntrôle de VITesse / Track Machine Transmission 430 Speed control)
        // Not implemented

        // Vigilance monitoring (VACMA)
        float VACMAActivationSpeedMpS;
        float VACMAPressedAlertDelayS = 55f;
        float VACMAPressedEmergencyDelayS = 60f;
        float VACMAReleasedAlertDelayS = 2.5f;
        float VACMAReleasedEmergencyDelayS = 5f;

        Timer VACMAPressedAlertTimer;
        Timer VACMAPressedEmergencyTimer;
        Timer VACMAReleasedAlertTimer;
        Timer VACMAReleasedEmergencyTimer;
        bool VACMAEmergencyBraking = false;
        bool VACMAPressed = false;

        // Other variables
        float PreviousSignalDistanceM = 0f;
        bool SignalPassed = false;

        public TCS_France() { }

        public override void Initialize()
        {
            if (!ElectroPneumaticBrake)
                BrakingEstablishedDelayS = 2f + 2f * (float)Math.Pow((double)TrainLengthM, 2D) * 0.00001f;
            else if (HeavyFreightTrain)
                BrakingEstablishedDelayS = 12f + TrainLengthM / 200f;

            KVBCurrentSignalSpeedLimitMpS = KVBTrainSpeedLimitMpS;
            KVBNextSignalSpeedLimitMpS = KVBTrainSpeedLimitMpS;
            KVBSignalTargetSpeedMpS = KVBTrainSpeedLimitMpS;
            KVBSignalTargetDistanceM = 0f;

            KVBCurrentSpeedPostSpeedLimitMpS = KVBTrainSpeedLimitMpS;
            KVBNextSpeedPostSpeedLimitMpS = KVBTrainSpeedLimitMpS;
            KVBSpeedPostTargetSpeedMpS = KVBTrainSpeedLimitMpS;
            KVBSpeedPostTargetDistanceM = 0f;

            KVBCurrentAlertSpeedMpS = MpS.FromKpH(5f);
            KVBCurrentEBSpeedMpS = MpS.FromKpH(10f);
            KVBNextAlertSpeedMpS = MpS.FromKpH(5f);
            KVBNextEBSpeedMpS = MpS.FromKpH(10f);

            VACMAActivationSpeedMpS = MpS.FromKpH(3f);
            VACMAPressedAlertTimer = new Timer(this);
            VACMAPressedAlertTimer.Setup(VACMAPressedAlertDelayS);
            VACMAPressedEmergencyTimer = new Timer(this);
            VACMAPressedEmergencyTimer.Setup(VACMAPressedEmergencyDelayS);
            VACMAReleasedAlertTimer = new Timer(this);
            VACMAReleasedAlertTimer.Setup(VACMAReleasedAlertDelayS);
            VACMAReleasedEmergencyTimer = new Timer(this);
            VACMAReleasedEmergencyTimer.Setup(VACMAReleasedEmergencyDelayS);

            Activated = true;
            PreviousSignalDistanceM = 0f;
        }

        public override void Update()
        {
            UpdateSignalPassed();

            if (!KVBPresent && !DAATPresent)
            {
                ActiveCCS = CCS.RSO;

                SetNextSignalAspect(Aspect.Clear_1);

                UpdateRSO();
                UpdateVACMA();
            }
            else if (!KVBPresent && DAATPresent)
            {
                ActiveCCS = CCS.DAAT;

                SetNextSignalAspect(Aspect.Clear_1);

                UpdateRSO();
                UpdateVACMA();
            }
            else if (CurrentPostSpeedLimitMpS() <= MpS.FromKpH(220f) && KVBPresent)
            {
                // Classic line = KVB active
                ActiveCCS = CCS.KVB;

                if (SignalPassed)
                    SetNextSignalAspect(NextSignalAspect(0));

                UpdateRSO();
                UpdateKVB();
                UpdateVACMA();
            }
            else
            {
                // High speed line = TVM active

                // Activation control (KAr) in KVB system
                if (TVM300Present)
                {
                    ActiveCCS = CCS.TVM300;

                    SetNextSignalAspect(NextSignalAspect(0));

                    UpdateTVM300();
                    UpdateRSO();
                    UpdateVACMA();
                }
                else
                {
                    // TVM not activated because not present
                    ActiveCCS = CCS.KVB;

                    if (SignalPassed)
                        SetNextSignalAspect(NextSignalAspect(0));

                    KVBEmergencyBraking = true;
                    SetPenaltyApplicationDisplay(true);
                    SetEmergency();
                }
            }

            RSOType1Inhibition = false; // No function for reverse
            RSOType2Inhibition = !KVBInhibited && ((TVM300Present && ActiveCCS == CCS.TVM300) || (TVM430Present && ActiveCCS == CCS.TVM430));
            RSOType3Inhibition = (TVM300Present || TVM430Present) && !TVMCOVITInhibited;

            PreviousCCS = ActiveCCS;
        }

        public override void SetEmergency()
        {
            SetPenaltyApplicationDisplay(true);
            SetEmergencyBrake();
            SetPantographsDown();
        }

        protected void UpdateRSO()
        {
            if (NextSignalDistanceM(0) < 2f && CurrentPostSpeedLimitMpS() <= MpS.FromKpH(220f))
            {
                if (NextSignalAspect(0) == Aspect.Stop
                    || NextSignalAspect(0) == Aspect.StopAndProceed
                    || NextSignalAspect(0) == Aspect.Restricted)
                    RSOClosedSignal = true;
                else if (NextSignalAspect(1) == Aspect.Stop
                    || NextSignalAspect(1) == Aspect.StopAndProceed)
                    RSOClosedSignal = true;
                else if (NextSignalSpeedLimitMpS(1) >= 0f && NextSignalSpeedLimitMpS(1) < MpS.FromKpH(160f))
                    RSOClosedSignal = true;
                else
                    RSOOpenedSignal = true;
            }
            if (SignalPassed)
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
            // Decode signal aspect
            switch (NextSignalAspect(0))
            {
                case Aspect.Stop:
                    KVBSignalTargetDistanceM = NextSignalDistanceM(0);
                    if (SignalPassed)
                    {
                        KVBNextSignalSpeedLimitMpS = MpS.FromKpH(10f);
                        KVBSignalTargetSpeedMpS = 0f;
                        KVBNextAlertSpeedMpS = MpS.FromKpH(2.5f);
                        KVBNextEBSpeedMpS = MpS.FromKpH(5f);
                    }
                    break;

                case Aspect.StopAndProceed:
                    KVBSignalTargetDistanceM = NextSignalDistanceM(0);
                    if (SignalPassed)
                    {
                        KVBNextSignalSpeedLimitMpS = MpS.FromKpH(30f);
                        KVBSignalTargetSpeedMpS = 0f;
                        KVBNextAlertSpeedMpS = MpS.FromKpH(5f);
                        KVBNextEBSpeedMpS = MpS.FromKpH(10f);
                    }
                    break;

                // Approach : Check if the 2nd signal is red (a yellow blinking aspect may have been crossed)
                case Aspect.Approach_1:
                case Aspect.Approach_2:
                case Aspect.Approach_3:
                    switch (NextSignalAspect(1))
                    {
                        case Aspect.Stop:
                            KVBSignalTargetDistanceM = NextSignalDistanceM(1);
                            if (SignalPassed)
                            {
                                if (NextSignalSpeedLimitMpS(0) > 0f && NextSignalSpeedLimitMpS(0) < KVBTrainSpeedLimitMpS)
                                    KVBNextSignalSpeedLimitMpS = NextSignalSpeedLimitMpS(0);
                                else
                                    KVBNextSignalSpeedLimitMpS = KVBTrainSpeedLimitMpS;
                                KVBSignalTargetSpeedMpS = 0f;
                                KVBNextAlertSpeedMpS = MpS.FromKpH(2.5f);
                                KVBNextEBSpeedMpS = MpS.FromKpH(5f);
                            }
                            break;

                        case Aspect.StopAndProceed:
                            KVBSignalTargetDistanceM = NextSignalDistanceM(1);
                            if (SignalPassed)
                            {
                                if (NextSignalSpeedLimitMpS(0) > 0f && NextSignalSpeedLimitMpS(0) < KVBTrainSpeedLimitMpS)
                                    KVBNextSignalSpeedLimitMpS = NextSignalSpeedLimitMpS(0);
                                else
                                    KVBNextSignalSpeedLimitMpS = KVBTrainSpeedLimitMpS;
                                KVBSignalTargetSpeedMpS = 0f;
                                KVBNextAlertSpeedMpS = MpS.FromKpH(5f);
                                KVBNextEBSpeedMpS = MpS.FromKpH(10f);
                            }
                            break;

                        default:
                            KVBSignalTargetDistanceM = NextSignalDistanceM(0);
                            if (SignalPassed)
                            {
                                if (NextSignalSpeedLimitMpS(0) > 0f && NextSignalSpeedLimitMpS(0) < KVBTrainSpeedLimitMpS)
                                    KVBNextSignalSpeedLimitMpS = NextSignalSpeedLimitMpS(0);
                                else
                                    KVBNextSignalSpeedLimitMpS = KVBTrainSpeedLimitMpS;
                                KVBSignalTargetSpeedMpS = KVBNextSignalSpeedLimitMpS;
                                KVBNextAlertSpeedMpS = MpS.FromKpH(5f);
                                KVBNextEBSpeedMpS = MpS.FromKpH(10f);
                            }
                            break;
                    }
                    break;

                // Clear 
                default:
                    KVBSignalTargetDistanceM = NextSignalDistanceM(0);
                    if (SignalPassed)
                    {
                        if (NextSignalSpeedLimitMpS(0) > 0f && NextSignalSpeedLimitMpS(0) < KVBTrainSpeedLimitMpS)
                            KVBNextSignalSpeedLimitMpS = NextSignalSpeedLimitMpS(0);
                        else
                            KVBNextSignalSpeedLimitMpS = KVBTrainSpeedLimitMpS;
                        KVBSignalTargetSpeedMpS = KVBNextSignalSpeedLimitMpS;
                        KVBNextAlertSpeedMpS = MpS.FromKpH(5f);
                        KVBNextEBSpeedMpS = MpS.FromKpH(10f);
                    }
                    break;
            }

            // Update current speed limit when speed is below the target or when the train approaches the signal
            if (NextSignalDistanceM(0) <= 5f)
            {
                if (NextSignalSpeedLimitMpS(0) > 0f && NextSignalSpeedLimitMpS(0) < KVBTrainSpeedLimitMpS)
                    KVBCurrentSignalSpeedLimitMpS = NextSignalSpeedLimitMpS(0);
                else
                    KVBCurrentSignalSpeedLimitMpS = KVBTrainSpeedLimitMpS;
            }

            // Speed post speed limit preparation

            KVBNextSpeedPostSpeedLimitMpS = (NextPostSpeedLimitMpS(0) > 0 ? NextPostSpeedLimitMpS(0) : KVBTrainSpeedLimitMpS);
            KVBCurrentSpeedPostSpeedLimitMpS = CurrentPostSpeedLimitMpS();
            KVBSpeedPostTargetSpeedMpS = KVBNextSpeedPostSpeedLimitMpS;
            KVBSpeedPostTargetDistanceM = NextPostDistanceM(0);

            SetNextSpeedLimitMpS(Math.Min(KVBNextSignalSpeedLimitMpS, KVBNextSpeedPostSpeedLimitMpS));
            SetCurrentSpeedLimitMpS(Math.Min(KVBCurrentSignalSpeedLimitMpS, KVBCurrentSpeedPostSpeedLimitMpS));

            UpdateKVBSpeedCurve();

            // Pre-announce aspect => KVB beep
            if (KVBCurrentSpeedPostSpeedLimitMpS > MpS.FromKpH(160f))
            {
                if (
                        SignalPassed
                        && KVBCurrentSignalSpeedLimitMpS > MpS.FromKpH(160f)
                        && KVBNextSignalSpeedLimitMpS <= MpS.FromKpH(160f)
                        && KVBPreAnnounceActive
                    )
                    KVBPreAnnounceActive = false;
                else if (
                        KVBNextSpeedPostSpeedLimitMpS <= MpS.FromKpH(160f)
                        && KVBSpeedPostTargetDistanceM < DistanceCurve(
                            KVBCurrentSpeedPostSpeedLimitMpS,
                            KVBNextSpeedPostSpeedLimitMpS,
                            KVBDeclivity,
                            BrakingEstablishedDelayS + KVBEmergencyBrakingAnticipationTimeS,
                            DecelerationMpS2
                        )
                        && KVBPreAnnounceActive
                    )
                    KVBPreAnnounceActive = false;
                else
                    KVBPreAnnounceActive = true;
            }
            else
                KVBPreAnnounceActive = false;

            if (!KVBPreAnnounceActive && KVBPreviousPreAnnounceActive)
                TriggerSoundInfo2();

            KVBPreviousPreAnnounceActive = KVBPreAnnounceActive;
        }

        protected void UpdateKVBSpeedCurve()
        {
            bool KVBOverspeed = false;
            
            KVBSignalEmergencySpeedCurveMpS =
                Math.Min( 
                    Math.Min(
                        Math.Max(
                            SpeedCurve(
                                KVBSignalTargetDistanceM,
                                KVBSignalTargetSpeedMpS,
                                KVBDeclivity,
                                BrakingEstablishedDelayS,
                                DecelerationMpS2
                            ),
                            KVBNextSignalSpeedLimitMpS + KVBNextEBSpeedMpS
                        ),
                        KVBTrainSpeedLimitMpS + MpS.FromKpH(10f)
                    ),
                    KVBCurrentSignalSpeedLimitMpS + KVBCurrentEBSpeedMpS
                );
            KVBSignalAlertSpeedCurveMpS =
                Math.Min(
                    Math.Min(
                        Math.Max(
                            SpeedCurve(
                                KVBSignalTargetDistanceM,
                                KVBSignalTargetSpeedMpS,
                                KVBDeclivity,
                                BrakingEstablishedDelayS + KVBEmergencyBrakingAnticipationTimeS,
                                DecelerationMpS2
                            ),
                            KVBNextSignalSpeedLimitMpS + KVBNextAlertSpeedMpS
                        ),
                        KVBTrainSpeedLimitMpS + MpS.FromKpH(5f)
                    ),
                    KVBCurrentSignalSpeedLimitMpS + KVBCurrentAlertSpeedMpS
                );
            KVBSpeedPostEmergencySpeedCurveMpS =
                Math.Min(
                    Math.Min(
                        Math.Max(
                            SpeedCurve(
                                KVBSpeedPostTargetDistanceM,
                                KVBSpeedPostTargetSpeedMpS,
                                KVBDeclivity,
                                BrakingEstablishedDelayS,
                                DecelerationMpS2
                            ),
                            KVBNextSpeedPostSpeedLimitMpS + MpS.FromKpH(10f)
                        ),
                        KVBTrainSpeedLimitMpS + MpS.FromKpH(10f)
                    ),
                    KVBCurrentSpeedPostSpeedLimitMpS + MpS.FromKpH(10f)
                );
            KVBSpeedPostAlertSpeedCurveMpS =
                Math.Min(
                    Math.Min(
                        Math.Max(
                            SpeedCurve(
                                KVBSpeedPostTargetDistanceM,
                                KVBSpeedPostTargetSpeedMpS,
                                KVBDeclivity,
                                BrakingEstablishedDelayS + KVBEmergencyBrakingAnticipationTimeS,
                                DecelerationMpS2
                            ),
                            KVBNextSpeedPostSpeedLimitMpS + MpS.FromKpH(5f)
                        ),
                        KVBTrainSpeedLimitMpS + MpS.FromKpH(5f)
                    ),
                    KVBCurrentSpeedPostSpeedLimitMpS + MpS.FromKpH(5f)
                );

            if (SpeedMpS() > KVBSignalAlertSpeedCurveMpS)
            {
                KVBOverspeed = true;

                if (SpeedMpS() > KVBSignalEmergencySpeedCurveMpS)
                {
                    KVBEmergencyBraking = true;
                    SetPenaltyApplicationDisplay(true);
                    SetEmergency();
                }
            }

            if (SpeedMpS() > KVBSpeedPostAlertSpeedCurveMpS)
            {
                KVBOverspeed = true;

                if (SpeedMpS() > KVBSpeedPostEmergencySpeedCurveMpS)
                {
                    KVBEmergencyBraking = true;
                    SetPenaltyApplicationDisplay(true);
                    SetEmergency();
                }
            }

            SetOverspeedWarningDisplay(KVBOverspeed);
            if (KVBOverspeed == true && KVBPreviousOverspeed == false)
                TriggerSoundPenalty1();
            KVBPreviousOverspeed = KVBOverspeed;

            if (KVBEmergencyBraking == true && KVBPreviousEmergencyBraking == false)
                TriggerSoundPenalty2();
            KVBPreviousEmergencyBraking = KVBEmergencyBraking;

            if (KVBEmergencyBraking)
            {
                if (SpeedMpS() >= 0.1f)
                    SetEmergency();
                else
                {
                    KVBEmergencyBraking = false;
                    SetPenaltyApplicationDisplay(false);
                }
            }
        }

        protected void UpdateTVM300()
        {
            TVM300NextSpeedLimitMpS = NextSignalSpeedLimitMpS(0);
            TVM300CurrentSpeedLimitMpS = CurrentSignalSpeedLimitMpS();

            if (TVM300CurrentSpeedLimitMpS > TVM300TrainSpeedLimitMpS)
                TVM300CurrentSpeedLimitMpS = TVM300TrainSpeedLimitMpS;

            if (TVM300CurrentSpeedLimitMpS < 0f)
                TVM300EmergencySpeedMpS = TVM300CurrentSpeedLimitMpS = TVM300NextSpeedLimitMpS;
            else if (TVM300CurrentSpeedLimitMpS <= MpS.FromKpH(80f))
                TVM300EmergencySpeedMpS = MpS.FromKpH(5f);
            else if (TVM300CurrentSpeedLimitMpS <= MpS.FromKpH(170f))
                TVM300EmergencySpeedMpS = MpS.FromKpH(10f);
            else
                TVM300EmergencySpeedMpS = MpS.FromKpH(15f);

            SetNextSpeedLimitMpS(TVM300NextSpeedLimitMpS);
            SetCurrentSpeedLimitMpS(TVM300CurrentSpeedLimitMpS);

            if (TVM300EmergencyBraking || SpeedMpS() > TVM300CurrentSpeedLimitMpS + TVM300EmergencySpeedMpS) {
                TVM300EmergencyBraking = true;
                SetPenaltyApplicationDisplay(true);
                SetEmergency();
            }
            if (TVM300EmergencyBraking && SpeedMpS() <= TVM300CurrentSpeedLimitMpS)
            {
                TVM300EmergencyBraking = false;
                SetPenaltyApplicationDisplay(false);
            }

            if (TVMPreviousAspect < NextSignalAspect(0) && SignalPassed)
                TVMClosedSignal = true;
            else
                TVMClosedSignal = false;

            if (TVMPreviousAspect > NextSignalAspect(0))
                TVMOpenedSignal = true;
            else
                TVMOpenedSignal = false;

            TVMPreviousAspect = NextSignalAspect(0);
        }

        protected void UpdateTVM430()
        {
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
            }
        }

        protected void UpdateVACMA()
        {
            if (!Activated || !IsAlerterEnabled() || SpeedMpS() < VACMAActivationSpeedMpS)
            {
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

            if ((VACMAPressedEmergencyTimer.Triggered || VACMAReleasedEmergencyTimer.Triggered) && !VACMAEmergencyBraking
                || SpeedMpS() >= 0.1f && VACMAEmergencyBraking)
            {
                VACMAEmergencyBraking = true;
                SetEmergencyBrake();
                SetPantographsDown();
            }
            if (VACMAEmergencyBraking && SpeedMpS() < 0.1f)
                VACMAEmergencyBraking = false;
        }

        protected void UpdateSignalPassed()
        {
            SignalPassed = NextSignalDistanceM(0) > PreviousSignalDistanceM && SpeedMpS() > 0;

            PreviousSignalDistanceM = NextSignalDistanceM(0);
        }
    }
}