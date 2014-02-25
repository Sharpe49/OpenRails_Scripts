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
            TVM300,     // RSO partially inhibited + TVM300
            TVM430,     // RSO partially inhibited + TVM430
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

        // DAAT (Dispositif d'Arrêt Automatique des Trains / Automatic Train Stop System)
        // Not implemented

        // KVB (Contrôle de Vitesse par Balises / Beacon speed control)
        bool KVBInhibited = false;
        float KVBEmergencyBrakingAnticipationTimeS = 5f;    // Tx
        float KVBTrainSpeedLimitMpS = MpS.FromKpH(220f);     // VT

        bool KVBPreviousOverspeed = false;
        bool KVBEmergencyBraking = false;
        bool KVBPreviousEmergencyBraking = false;

        float KVBPreviousSignalDistanceM;                   // D
        float KVBCurrentSignalSpeedLimitMpS;
        float KVBNextSignalSpeedLimitMpS;
        float KVBSignalTargetSpeedMpS;
        float KVBSignalTargetDistanceM;
        float KVBDeclivity = 0f;                            // i

        float KVBCurrentSpeedPostSpeedLimitMpS;
        float KVBNextSpeedPostSpeedLimitMpS;
        float KVBSpeedPostTargetSpeedMpS;
        float KVBSpeedPostTargetDistanceM;
        float KVBAlertSpeedMpS;
        float KVBEBSpeedMpS;

        float KVBSignalEmergencySpeedCurveMpS;
        float KVBSignalAlertSpeedCurveMpS;
        float KVBSpeedPostEmergencySpeedCurveMpS;
        float KVBSpeedPostAlertSpeedCurveMpS;

        // TVM300 COVIT (Transmission Voie Machine 300 COntrôle de VITesse / Track Machine Transmission 300 Speed control)
        bool TVMCOVITInhibited = false;
        float TVM300TrainSpeedLimitMpS = MpS.FromKpH(300f);

        float TVM300CurrentSpeedLimitMpS;
        float TVM300NextSpeedLimitMpS;
        float TVM300EmergencySpeedMpS;
        bool TVM300EmergencyBraking;

        // TVM430 COVIT (Transmission Voie Machine 430 COntrôle de VITesse / Track Machine Transmission 430 Speed control)
        // Not implemented

        // Vigilance monitoring (VACMA)
        bool VigilanceAlarm = false;
        bool VigilanceEmergency = false;

        public TCS_France() { }

        public override void Initialize()
        {
            if (!ElectroPneumaticBrake)
                BrakingEstablishedDelayS = 2f + 2f * (float)Math.Pow((double)TrainLengthM, 2D) * 0.00001f;
            else if (HeavyFreightTrain)
                BrakingEstablishedDelayS = 12f + TrainLengthM / 200f;

            KVBPreviousSignalDistanceM = 0f;
            KVBCurrentSignalSpeedLimitMpS = KVBTrainSpeedLimitMpS;
            KVBNextSignalSpeedLimitMpS = KVBTrainSpeedLimitMpS;
            KVBSignalTargetSpeedMpS = KVBTrainSpeedLimitMpS;
            KVBSignalTargetDistanceM = 0f;

            KVBCurrentSpeedPostSpeedLimitMpS = KVBTrainSpeedLimitMpS;
            KVBNextSpeedPostSpeedLimitMpS = KVBTrainSpeedLimitMpS;
            KVBSpeedPostTargetSpeedMpS = KVBTrainSpeedLimitMpS;
            KVBSpeedPostTargetDistanceM = 0f;

            KVBAlertSpeedMpS = MpS.FromKpH(5);
            KVBEBSpeedMpS = MpS.FromKpH(10);

            Activated = true;
        }

        public override void Update()
        {
            SetNextSignalAspect(NextSignalAspect(0));

            if (!KVBPresent && !DAATPresent)
            {
                ActiveCCS = CCS.RSO;

                UpdateVACMA();
            }
            else if (!KVBPresent && DAATPresent)
            {
                ActiveCCS = CCS.DAAT;

                UpdateVACMA();
            }
            if (CurrentPostSpeedLimitMpS() <= MpS.FromKpH(220f) && KVBPresent)
            {
                // Classic line = KVB active
                ActiveCCS = CCS.KVB;

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

                    UpdateTVM300();
                }
                else
                {
                    // TVM not activated because not present
                    ActiveCCS = CCS.KVB;

                    KVBEmergencyBraking = true;
                    SetPenaltyApplicationDisplay(true);
                    SetEmergency();
                }
            }
        }

        public override void SetEmergency()
        {
            SetPenaltyApplicationDisplay(true);
            if (IsBrakeEmergency())
                return;
            SetEmergencyBrake();

            SetThrottleController(0.0f); // Necessary while second locomotive isn't switched off during EB.
            SetPantographsDown();
        }

        protected void UpdateRSO()
        {
        }

        protected void UpdateKVB()
        {
            // Decode signal aspect
            if (NextSignalDistanceM(0) > KVBPreviousSignalDistanceM)
            {
                switch (NextSignalAspect(0))
                {
                    case Aspect.Stop:
                        KVBNextSignalSpeedLimitMpS = MpS.FromKpH(10f);
                        KVBSignalTargetSpeedMpS = 0f;
                        KVBAlertSpeedMpS = MpS.FromKpH(2.5f);
                        KVBEBSpeedMpS = MpS.FromKpH(5f);
                        break;

                    case Aspect.StopAndProceed:
                        KVBNextSignalSpeedLimitMpS = MpS.FromKpH(30f);
                        KVBSignalTargetSpeedMpS = 0f;
                        KVBAlertSpeedMpS = MpS.FromKpH(5f);
                        KVBEBSpeedMpS = MpS.FromKpH(10f);
                        break;

                    case Aspect.Clear_1:
                    case Aspect.Clear_2:
                    case Aspect.Approach_1:
                    case Aspect.Approach_2:
                    case Aspect.Approach_3:
                    case Aspect.Restricted:
                        if (NextSignalSpeedLimitMpS(0) > 0f && NextSignalSpeedLimitMpS(0) < KVBTrainSpeedLimitMpS)
                            KVBNextSignalSpeedLimitMpS = NextSignalSpeedLimitMpS(0);
                        else
                            KVBNextSignalSpeedLimitMpS = KVBTrainSpeedLimitMpS;
                        KVBSignalTargetSpeedMpS = KVBNextSignalSpeedLimitMpS;
                        KVBAlertSpeedMpS = MpS.FromKpH(5f);
                        KVBEBSpeedMpS = MpS.FromKpH(10f);
                        break;
                }
            }
            KVBPreviousSignalDistanceM = NextSignalDistanceM(0);
            KVBSignalTargetDistanceM = NextSignalDistanceM(0);

            // Update current speed limit when speed is below the target or when the train approaches the signal
            if (NextSignalDistanceM(0) <= 10f)
                KVBCurrentSignalSpeedLimitMpS = KVBNextSignalSpeedLimitMpS;

            // Speed post speed limit preparation

            KVBNextSpeedPostSpeedLimitMpS = (NextPostSpeedLimitMpS(0) > 0 ? NextPostSpeedLimitMpS(0) : KVBTrainSpeedLimitMpS);
            KVBCurrentSpeedPostSpeedLimitMpS = CurrentPostSpeedLimitMpS();
            KVBSpeedPostTargetSpeedMpS = KVBNextSpeedPostSpeedLimitMpS;
            KVBSpeedPostTargetDistanceM = NextPostDistanceM(0);

            SetNextSpeedLimitMpS(Math.Min(KVBNextSignalSpeedLimitMpS, KVBNextSpeedPostSpeedLimitMpS));
            SetCurrentSpeedLimitMpS(Math.Min(KVBCurrentSignalSpeedLimitMpS, KVBCurrentSpeedPostSpeedLimitMpS));

            UpdateKVBSpeedCurve();
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
                            KVBNextSignalSpeedLimitMpS + KVBEBSpeedMpS
                        ),
                        KVBTrainSpeedLimitMpS + MpS.FromKpH(10f)
                    ),
                    KVBCurrentSignalSpeedLimitMpS + KVBEBSpeedMpS
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
                            KVBNextSignalSpeedLimitMpS + KVBAlertSpeedMpS
                        ),
                        KVBTrainSpeedLimitMpS + MpS.FromKpH(5f)
                    ),
                    KVBCurrentSignalSpeedLimitMpS + KVBAlertSpeedMpS
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
        }

        protected void UpdateTVM430()
        {
        }

        public override void AlerterReset()
        {
        }

        public override void AlerterPressed()
        {
            if (!Activated || VigilanceEmergency)
                return;
        }

        protected void UpdateVACMA()
        {
            if (!IsAlerterEnabled())
                return;
        }
    }
}