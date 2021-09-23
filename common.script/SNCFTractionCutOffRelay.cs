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

    public class SNCFTractionCutOffRelay : TractionCutOffRelay
    {
        private Timer ClosingTimer;
        private TractionCutOffRelayState PreviousState;

        public override void Initialize()
        {
            ClosingTimer = new Timer(this);
            ClosingTimer.Setup(ClosingDelayS());

            SetDriverOpeningOrder(false);
            SetDriverClosingAuthorization(false);
        }

        public override void Update(float elapsedSeconds)
        {
            SetClosingAuthorization(TCSClosingAuthorization() && CurrentDieselEngineState() == DieselEngineState.Running);

            switch (CurrentState())
            {
                case TractionCutOffRelayState.Closed:
                    if (!ClosingAuthorization())
                    {
                        SetCurrentState(TractionCutOffRelayState.Open);
                    }
                    break;

                case TractionCutOffRelayState.Closing:
                    if (ClosingAuthorization() && DriverClosingOrder())
                    {
                        if (!ClosingTimer.Started)
                        {
                            ClosingTimer.Start();
                        }

                        if (ClosingTimer.Triggered)
                        {
                            ClosingTimer.Stop();
                            SetCurrentState(TractionCutOffRelayState.Closed);
                        }
                    }
                    else
                    {
                        ClosingTimer.Stop();
                        SetCurrentState(TractionCutOffRelayState.Open);
                    }
                    break;

                case TractionCutOffRelayState.Open:
                    if (ClosingAuthorization() && DriverClosingOrder())
                    {
                        SetCurrentState(TractionCutOffRelayState.Closing);
                    }
                    break;
            }

            if (PreviousState != CurrentState())
            {
                switch (CurrentState())
                {
                    case TractionCutOffRelayState.Open:
                        SignalEvent(Event.TractionCutOffRelayOpen);
                        break;

                    case TractionCutOffRelayState.Closing:
                        SignalEvent(Event.TractionCutOffRelayClosing);
                        break;

                    case TractionCutOffRelayState.Closed:
                        SignalEvent(Event.TractionCutOffRelayClosed);
                        break;
                }
            }

            PreviousState = CurrentState();
        }

        public override void HandleEvent(PowerSupplyEvent evt)
        {
            switch (evt)
            {
                case PowerSupplyEvent.CloseTractionCutOffRelayButtonPressed:
                    if (!DriverClosingOrder())
                    {
                        SetDriverClosingOrder(true);
                        SignalEvent(Event.TractionCutOffRelayClosingOrderOn);
                        Confirm(CabControl.TractionCutOffRelayClosingOrder, CabSetting.On);
                        if (!ClosingAuthorization())
                        {
                            if (CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "fr")
                            {
                                Message(ConfirmLevel.Warning, "Fermeture du relai de traction non autorisée");
                            }
                            else
                            {
                                Message(ConfirmLevel.Warning, "Traction cut-off relay closing not authorized");
                            }
                        }
                    }
                    break;

                case PowerSupplyEvent.CloseTractionCutOffRelayButtonReleased:
                    if (DriverClosingOrder())
                    {
                        SetDriverClosingOrder(false);
                        SignalEvent(Event.TractionCutOffRelayClosingOrderOff);
                    }
                    break;

                case PowerSupplyEvent.QuickPowerOn:
                    SetCurrentState(TractionCutOffRelayState.Closed);
                    break;

                case PowerSupplyEvent.QuickPowerOff:
                    SetCurrentState(TractionCutOffRelayState.Open);
                    break;
            }
        }
    }

}