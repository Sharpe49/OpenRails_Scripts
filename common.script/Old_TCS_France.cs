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

using Orts.Simulation.RollingStocks;
using ORTS.Common;
using ORTS.Scripting.Api;
using System;
using System.Collections.Generic;

namespace ORTS.Scripting.Script
{
    public class Old_TCS_France : TrainControlSystem
    {
        // Cabview control number
        // Not sure about the names of the buttons and lights for RS (old system)
        const int BP_AC_SF = 0;
        const int BP_A_LS_SF = 2;
        const int Z_ES_VA = 3;
        const int BP_AM_V1 = 9;
        const int BP_AM_V2 = 10;
        const int BP_DM = 11;
        const int LS_SF = 32;
        const int TVM_Mask = 47;

        enum RSStateType
        {
            Off,
            TriggeredSounding,
            TriggeredBlinking
        }

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
        bool RSPresent;                                     // RS
        bool DAATPresent;                                   // DAAT
        bool TVM300Present;                                 // TVM300

    // RS (Répétition des Signaux / Signal Repetition)
        // Parameters
        float RSDelayBeforeEmergencyBrakingS;
        float RSBlinkerFrequencyHz;

        // Variables
        RSStateType RSState = RSStateType.TriggeredBlinking;
        Aspect RSLastSignalAspect = Aspect.Clear_1;
        bool RSEmergencyBraking = true;
        bool RSPressed = false;
        bool RSPreviousPressed = false;
        bool RSCancelPressed = false;
        bool RSType1Inhibition = false;                     // Inhibition 1 : Reverse
        bool RSType2Inhibition = false;                     // Inhibition 2 : Train on HSL
        bool RSType3Inhibition = false;                     // Inhibition 3 : TVM COVIT not inhibited
        bool RSClosedSignal = false;
        bool RSPreviousClosedSignal = false;
        bool RSOpenedSignal = false;
        bool RSPreviousOpenedSignal = false;
        Blinker RSBlinker;
        Timer RSEmergencyTimer;

    // DAAT (Dispositif d'Arrêt Automatique des Trains / Automatic Train Stop System)
        // Not implemented

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

        public Old_TCS_France() { }

        public override void Initialize()
        {
            // General section
            VACMAPresent = GetBoolParameter("General", "VACMAPresent", true);
            RSPresent = GetBoolParameter("General", "RSPresent", true);
            DAATPresent = GetBoolParameter("General", "DAATPresent", false);
            TVM300Present = GetBoolParameter("General", "TVM300Present", false);

            // RS section
            RSDelayBeforeEmergencyBrakingS = GetFloatParameter("RS", "DelayBeforeEmergencyBrakingS", 4f);
            RSBlinkerFrequencyHz = GetFloatParameter("RS", "BlinkerFrequencyHz", 1f);

            // TVM common section
            TVMCOVITInhibited = GetBoolParameter("TVM", "CovitInhibited", false);

            // TVM300 section
            TVM300TrainSpeedLimitMpS = MpS.FromKpH(GetFloatParameter("TVM300", "TrainSpeedLimitKpH", 300f));

            // VACMA section
            VACMAActivationSpeedMpS = MpS.FromKpH(GetFloatParameter("VACMA", "ActivationSpeedKpH", 3f));
            VACMAReleasedAlertDelayS = GetFloatParameter("VACMA", "ReleasedAlertDelayS", 2.5f);
            VACMAReleasedEmergencyDelayS = GetFloatParameter("VACMA", "ReleasedEmergencyDelayS", 5f);
            VACMAPressedAlertDelayS = GetFloatParameter("VACMA", "PressedAlertDelayS", 55f);
            VACMAPressedEmergencyDelayS = GetFloatParameter("VACMA", "PressedEmergencyDelayS", 60f);

            // Variables initialization
            RSBlinker = new Blinker(this);
            RSBlinker.Setup(RSBlinkerFrequencyHz);
            RSBlinker.Start();
            RSEmergencyTimer = new Timer(this);
            RSEmergencyTimer.Setup(RSDelayBeforeEmergencyBrakingS);
            VACMAPressedAlertTimer = new Timer(this);
            VACMAPressedAlertTimer.Setup(VACMAPressedAlertDelayS);
            VACMAPressedEmergencyTimer = new Timer(this);
            VACMAPressedEmergencyTimer.Setup(VACMAPressedEmergencyDelayS);
            VACMAReleasedAlertTimer = new Timer(this);
            VACMAReleasedAlertTimer.Setup(VACMAReleasedAlertDelayS);
            VACMAReleasedEmergencyTimer = new Timer(this);
            VACMAReleasedEmergencyTimer.Setup(VACMAReleasedEmergencyDelayS);

            // Cabview control names initialization
            SetCustomizedCabviewControlName(BP_AC_SF, "BP (AC) SF : Acquittement / Acknowledge");
            SetCustomizedCabviewControlName(BP_A_LS_SF, "BP (A) LS (SF) : Annulation LS (SF) / Cancel LS (SF)");
            SetCustomizedCabviewControlName(Z_ES_VA, "Z (ES) VA : Essai VACMA / Alerter test");
            SetCustomizedCabviewControlName(BP_AM_V1, "BP AM V1 : Armement manuel TVM voie 1 / TVM manual arming track 1");
            SetCustomizedCabviewControlName(BP_AM_V2, "BP AM V2 : Armement manuel TVM voie 2 / TVM manual arming track 2");
            SetCustomizedCabviewControlName(BP_DM, "BP DM : Désarmement manuel TVM / TVM manual dearming");
            SetCustomizedCabviewControlName(LS_SF, "LS (SF) : Signal Fermé / Closed Signal");
            SetCustomizedCabviewControlName(TVM_Mask, "Masque TVM / TVM mask");

            Activated = true;

            SetNextSignalAspect(Aspect.Clear_1);
        }

        public override void Update()
        {
            if (InitCount < 5)
            {
                InitCount++;

                if (InitCount == 5 && SpeedMpS() > 0f)
                {
                    RSState = RSStateType.Off;
                    RSEmergencyBraking = false;
                    VACMAEmergencyBraking = false;
                }

                return;
            }

            UpdateSignalPassed();

            UpdateVACMA();

            if (IsTrainControlEnabled() && IsSpeedControlEnabled())
            {
                if (RSPresent)
                {
                    UpdateRS();
                }

                if (TVM300Present)
                {
                    UpdateTVM();
                }

                if (RSEmergencyBraking
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

                RSType1Inhibition = IsDirectionReverse();
                RSType2Inhibition = TVM300Present && TVMArmed;
                RSType3Inhibition = !TVM300Present || !TVMCOVITInhibited;

                PreviousLineSpeed = CurrentPostSpeedLimitMpS();
            }
        }

        public override void SetEmergency(bool emergency)
        {
            ExternalEmergencyBraking = emergency;
        }

        protected void UpdateRS()
        {
            // If train is about to cross a normal signal, get its information.
            float nextNormalSignalDistance = NextSignalDistanceM(0);
            if (nextNormalSignalDistance <= 5f)
            {
                RSLastSignalAspect = NextSignalAspect(0);
            }

            // If train is about to cross a normal signal, get its information.
            float nextDistantSignalDistance = NextDistanceSignalDistanceM();
            if (nextDistantSignalDistance <= 5f)
            {
                RSLastSignalAspect = NextDistanceSignalAspect();
            }

            RSClosedSignal = RSOpenedSignal = false;

            if ((NormalSignalPassed || DistantSignalPassed)
                && !RSType1Inhibition
                && !TVMArmed
                && SpeedMpS() > 0.1f)
            {
                if (RSLastSignalAspect == Aspect.Stop
                    || RSLastSignalAspect == Aspect.StopAndProceed
                    || RSLastSignalAspect == Aspect.Restricted
                    || RSLastSignalAspect == Aspect.Approach_1
                    || RSLastSignalAspect == Aspect.Approach_2
                    || RSLastSignalAspect == Aspect.Approach_3
                    )
                {
                    RSClosedSignal = true;
                }
                else
                {
                    RSOpenedSignal = true;
                }
            }

            switch (RSState)
            {
                case RSStateType.Off:
                    SetCabDisplayControl(LS_SF, 0);
                    if ((RSClosedSignal && !RSType2Inhibition) || (TVMClosedSignal && !RSType3Inhibition))
                    {
                        if (RSPressed)
                        {
                            RSState = RSStateType.TriggeredBlinking;
                            RSBlinker.Start();
                        }
                        else
                        {
                            RSState = RSStateType.TriggeredSounding;
                            RSBlinker.Start();
                            RSEmergencyTimer.Start();
                        }
                    }
                    break;

                case RSStateType.TriggeredSounding:
                    // LS (SF)
                    SetCabDisplayControl(LS_SF, RSBlinker.On ? 1 : 0);

                    if (!RSPressed && RSPreviousPressed)
                    {
                        RSState = RSStateType.TriggeredBlinking;
                        RSEmergencyTimer.Stop();
                    }

                    if (RSOpenedSignal || TVMOpenedSignal || RSCancelPressed)
                    {
                        RSState = RSStateType.Off;
                        RSEmergencyTimer.Stop();
                        RSBlinker.Stop();
                    }
                    break;

                case RSStateType.TriggeredBlinking:
                    SetCabDisplayControl(LS_SF, RSBlinker.On ? 1 : 0);

                    if ((RSClosedSignal && !RSType2Inhibition) || (TVMClosedSignal && !RSType3Inhibition))
                    {
                        if (RSPressed)
                        {
                            RSState = RSStateType.TriggeredBlinking;
                        }
                        else
                        {
                            RSState = RSStateType.TriggeredSounding;
                            RSEmergencyTimer.Start();
                        }
                    }

                    if (RSOpenedSignal || TVMOpenedSignal || RSCancelPressed)
                    {
                        RSState = RSStateType.Off;
                        RSBlinker.Stop();
                    }
                    break;
            }

            if (RSEmergencyTimer.Triggered)
            {
                RSEmergencyBraking = true;
            }
            else if (RearmingButton)
            {
                RSEmergencyBraking = false;
            }

            if (RSClosedSignal && !RSPreviousClosedSignal && !RSType1Inhibition)
            {
                TriggerSoundPenalty1();
            }

            if (RSOpenedSignal && !RSPreviousOpenedSignal && !RSType1Inhibition)
            {
                TriggerSoundPenalty2();
                TriggerSoundInfo1();
            }

            RSPreviousClosedSignal = RSClosedSignal;
            RSPreviousOpenedSignal = RSOpenedSignal;

            if (TVM300Present)
            {
                if (TVMClosedSignal && !TVMPreviousClosedSignal)
                {
                    TriggerSoundPenalty1();
                }

                if (TVMOpenedSignal && !TVMPreviousOpenedSignal)
                {
                    TriggerSoundPenalty2();
                    TriggerSoundInfo1();
                }

                TVMPreviousClosedSignal = TVMClosedSignal;
                TVMPreviousOpenedSignal = TVMOpenedSignal;
            }

            if ((!RSPressed && RSPreviousPressed) || RSCancelPressed)
            {
                TriggerSoundPenalty2();
            }

            RSPreviousPressed = RSPressed;
        }

        protected void UpdateTVM()
        {
            // Automatic arming
            if (CurrentPostSpeedLimitMpS() > MpS.FromKpH(221f) && PreviousLineSpeed <= MpS.FromKpH(221f) && SpeedMpS() > 0f && !TVMArmed)
            {
                TVMArmed = true;

                TVMPreviousAspect = NextSignalAspect(0);
                TVMAspect = NextSignalAspect(0);
                SetNextSignalAspect(NextSignalAspect(0));
            }

            // Automatic dearming
            if (CurrentPostSpeedLimitMpS() <= MpS.FromKpH(221f) && PreviousLineSpeed > MpS.FromKpH(221f) && SpeedMpS() > 0f && TVMArmed)
            {
                TVMArmed = false;
            }

            if (TVMArmed)
            {
                // TVM mask
                SetCabDisplayControl(TVM_Mask, 1);

                UpdateTVM300Display();
                UpdateTVM300COVIT();
            }
            else
            {
                // TVM mask
                SetCabDisplayControl(TVM_Mask, 0);

                TVMAspect = Aspect.None;
                TVMPreviousAspect = Aspect.None;
            }
        }

        protected void UpdateTVM300Display()
        {
            UpdateTVMAspect(NextSignalAspect(0));
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
                                    RSPressed = true;
                                    break;

                                // BP (A) LS (SF)
                                case BP_A_LS_SF:
                                    RSCancelPressed = true;
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
                                    RSPressed = false;
                                    break;

                                // BP (A) LS (SF)
                                case BP_A_LS_SF:
                                    RSCancelPressed = false;
                                    break;

                                // BP AM V1 and BP AM V2
                                case BP_AM_V1:
                                case BP_AM_V2:
                                    TVMArmed = true;
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
            if (VACMAPresent && Activated && IsTrainControlEnabled() && IsAlerterEnabled())
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