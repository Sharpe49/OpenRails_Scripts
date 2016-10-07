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
using ORTS;
using Orts.Common;
using ORTS.Scripting.Api;
using Orts.Simulation;

namespace ORTS.Scripting.Script
{

    public class SNCFCircuitBreaker : CircuitBreaker
    {
        private Timer ClosingTimer;

        public override void Initialize()
        {
            ClosingTimer = new Timer(this);
            ClosingTimer.Setup(ClosingDelayS());
        }

        public override void Update(float elapsedSeconds)
        {
            SetClosingAuthorization(TCSClosingAuthorization() && DriverClosingAuthorization() && CurrentPantographState() == PantographState.Up);

            switch (CurrentState())
            {
                case CircuitBreakerState.Closed:
                    if (!ClosingAuthorization())
                    {
                        SetCurrentState(CircuitBreakerState.Open);
                    }
                    break;

                case CircuitBreakerState.Closing:
                    if (ClosingAuthorization() && DriverClosingOrder())
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
                    if (ClosingAuthorization() && DriverClosingOrder())
                    {
                        SetCurrentState(CircuitBreakerState.Closing);
                    }
                    break;
            }
        }

        public override void HandleEvent(PowerSupplyEvent evt)
        {
            switch (evt)
            {
                case PowerSupplyEvent.CloseCircuitBreakerButtonPressed:
                    SetDriverClosingOrder(true);
                    SignalEvent(Event.CircuitBreakerClosingOrderOn);
                    Confirm(CabControl.CircuitBreakerClosingOrder, CabSetting.On);
                    if (!ClosingAuthorization())
                    {
                        Message(ConfirmLevel.Warning, "Circuit breaker closing not authorized / Fermeture du disjoncteur non autorisée");
                    }
                    break;

                case PowerSupplyEvent.CloseCircuitBreakerButtonReleased:
                    SetDriverClosingOrder(false);
                    SignalEvent(Event.CircuitBreakerClosingOrderOff);
                    break;

                case PowerSupplyEvent.GiveCircuitBreakerClosingAuthorization:
                    SetDriverClosingAuthorization(true);
                    SignalEvent(Event.CircuitBreakerClosingAuthorizationOn);
                    Confirm(CabControl.CircuitBreakerClosingAuthorization, CabSetting.On);
                    break;

                case PowerSupplyEvent.RemoveCircuitBreakerClosingAuthorization:
                    SetDriverClosingAuthorization(false);
                    SignalEvent(Event.CircuitBreakerClosingAuthorizationOff);
                    Confirm(CabControl.CircuitBreakerClosingAuthorization, CabSetting.Off);
                    break;
            }
        }
    }

}