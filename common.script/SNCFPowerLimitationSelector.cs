// COPYRIGHT 2022 by the Open Rails project.
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
using ORTS.Scripting.Api;
using Orts.Simulation;

namespace ORTS.Scripting.Script
{
    public class SNCFPowerLimitationSelector : PowerLimitationSelector
    {
        public override void HandleEvent(PowerSupplyEvent evt)
        {
            switch (evt)
            {
                case PowerSupplyEvent.IncreasePowerLimitationSelectorPosition:
                    IncreasePosition();
                    break;

                case PowerSupplyEvent.DecreasePowerLimitationSelectorPosition:
                    DecreasePosition();
                    break;
            }
        }

        private void IncreasePosition()
        {
            int index = Positions.IndexOf(Position);

            if (index < Positions.Count - 1)
            {
                index++;

                Position = Positions[index];

                SignalEvent(Event.PowerLimitationSelectorIncrease);
                Message(CabControl.None, "Selecteur de puissance MP(CO)P en position " + Position.Name);
            }
        }

        private void DecreasePosition()
        {
            int index = Positions.IndexOf(Position);

            if (index > 0)
            {
                index--;

                Position = Positions[index];

                SignalEvent(Event.PowerLimitationSelectorDecrease);
                Message(CabControl.None, "Selecteur de puissance MP(CO)P en position " + Position.Name);
            }
        }
    }
}
