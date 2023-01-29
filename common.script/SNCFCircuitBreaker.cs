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

using System.Globalization;
using System.Net;
using Orts.Common;
using ORTS.Common;
using Orts.Simulation;
using ORTS.Scripting.Api;

namespace ORTS.Scripting.Script
{

    public class SNCFCircuitBreaker : CircuitBreaker
    {
        private Timer ClosingTimer;
        private CircuitBreakerState PreviousState;

        private Timer InitTimer;
        private bool Init = true;
        private bool Authorization = false;
        private bool TCSOpening = false;

        public override void Initialize()
        {
            ClosingTimer = new Timer(this);
            ClosingTimer.Setup(ClosingDelayS());

            InitTimer = new Timer(this);
            InitTimer.Setup(5f);

            if (ServiceRetentionActive)
            {
                SetCurrentState(CircuitBreakerState.Closed);
            }
            else
            {
                Init = false;
            }
        }

        public override void Update(float elapsedSeconds)
        {
            if (Init)
            {
                if (!InitTimer.Started)
                {
                    InitTimer.Start();
                    SignalEvent(Event.CircuitBreakerClosed);
                }

                if (InitTimer.Triggered)
                {
                    InitTimer.Stop();
                    Init = false;
                }

                return;
            }

            Authorization = TCSClosingAuthorization() && DriverClosingAuthorization() && CurrentPantographState() == PantographState.Up;

            if (TCSOpeningOrder())
            {
                TCSOpening = true;
            }
            else if (CurrentState() == CircuitBreakerState.Closed)
            {
                TCSOpening = false;
            }

            SetClosingAuthorization(CurrentPantographState() == PantographState.Up && SpeedMpS() < MpS.FromKpH(3f)); // Arbitrary voltage value


            switch (CurrentState())
            {
                case CircuitBreakerState.Closed:
                    if (!Authorization && !ServiceRetentionActive
                        || TCSOpeningOrder()
                        || CurrentPantographState() != PantographState.Up)
                    {
                        SetCurrentState(CircuitBreakerState.Open);
                    }
                    break;

                case CircuitBreakerState.Closing:
                    if (Authorization && (DriverClosingOrder() || TCSClosingOrder()))
                    {
                        if (!ClosingTimer.Started)
                        {
                            ClosingTimer.Start();
                        }

                        if (ClosingTimer.Triggered)
                        {
                            ClosingTimer.Stop();
                            SetCurrentState(CircuitBreakerState.Closed);
                        }
                    }
                    else
                    {
                        ClosingTimer.Stop();
                        SetCurrentState(CircuitBreakerState.Open);
                    }
                    break;

                case CircuitBreakerState.Open:
                    if (Authorization && (DriverClosingOrder() || TCSClosingOrder()))
                    {
                        SetCurrentState(CircuitBreakerState.Closing);
                    }
                    break;
            }

            if (PreviousState != CurrentState())
            {
                switch (CurrentState())
                {
                    case CircuitBreakerState.Open:
                        SignalEvent(Event.CircuitBreakerOpen);
                        break;

                    case CircuitBreakerState.Closing:
                        SignalEvent(Event.CircuitBreakerClosing);
                        break;

                    case CircuitBreakerState.Closed:
                        SignalEvent(Event.CircuitBreakerClosed);
                        break;
                }
            }

            PreviousState = CurrentState();
        }

        public override void HandleEvent(PowerSupplyEvent evt)
        {
            switch (evt)
            {
                case PowerSupplyEvent.CloseCircuitBreakerButtonPressed:
                    if (!DriverClosingOrder())
                    {
                        SetDriverClosingOrder(true);
                        SignalEvent(Event.CircuitBreakerClosingOrderOn);
                        Confirm(CabControl.CircuitBreakerClosingOrder, CabSetting.On);
                        if (!ClosingAuthorization())
                        {
                            if (CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "fr")
                            {
                                Message(ConfirmLevel.Warning, "Fermeture du disjoncteur non autorisée");
                            }
                            else
                            {
                                Message(ConfirmLevel.Warning, "Circuit breaker closing not authorized");
                            }
                        }
                    }
                    break;

                case PowerSupplyEvent.CloseCircuitBreakerButtonReleased:
                    if (DriverClosingOrder())
                    {
                        SetDriverClosingOrder(false);
                        SignalEvent(Event.CircuitBreakerClosingOrderOff);
                    }
                    break;

                case PowerSupplyEvent.GiveCircuitBreakerClosingAuthorization:
                    if (!DriverClosingAuthorization())
                    {
                        SetDriverClosingAuthorization(true);
                        SignalEvent(Event.CircuitBreakerClosingAuthorizationOn);
                        Confirm(CabControl.CircuitBreakerClosingAuthorization, CabSetting.On);
                    }
                    break;

                case PowerSupplyEvent.RemoveCircuitBreakerClosingAuthorization:
                    if (DriverClosingAuthorization())
                    {
                        SetDriverClosingAuthorization(false);
                        SignalEvent(Event.CircuitBreakerClosingAuthorizationOff);
                        Confirm(CabControl.CircuitBreakerClosingAuthorization, CabSetting.Off);
                    }
                    break;

                case PowerSupplyEvent.QuickPowerOn:
                    SetDriverClosingAuthorization(true);
                    SetCurrentState(CircuitBreakerState.Closed);
                    break;

                case PowerSupplyEvent.QuickPowerOff:
                    SetDriverClosingAuthorization(false);
                    SetCurrentState(CircuitBreakerState.Open);
                    break;
            }
        }
    }

}