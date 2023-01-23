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
    public class SNCFVoltageSelector : VoltageSelector
    {
        public override void HandleEvent(PowerSupplyEvent evt)
        {
            switch (evt)
            {
                case PowerSupplyEvent.IncreaseVoltageSelectorPosition:
                    IncreasePosition();
                    break;

                case PowerSupplyEvent.DecreaseVoltageSelectorPosition:
                    DecreasePosition();
                    break;
            }
        }

        public override void HandleEvent(PowerSupplyEvent evt, int id)
        {
            switch (evt)
            {
                case PowerSupplyEvent.RaisePantograph:
                case PowerSupplyEvent.LowerPantograph:
                    switch (id)
                    {
                        case 3:
                            IncreasePosition();
                            break;

                        case 4:
                            DecreasePosition();
                            break;
                    }
                    break;
            }
        }

        private void IncreasePosition()
        {
            if (PantographSelectorPosition.Name != "0")
            {
                Message(CabControl.None, "Selecteur de tension verrouillé - Baissez le pantographe pour changer de tension");
                return;
            }

            int index = Positions.IndexOf(Position);

            if (index < Positions.Count - 1)
            {
                index++;

                Position = Positions[index];

                SignalEvent(Event.VoltageSelectorIncrease);
                Message(CabControl.None, "Selecteur de tension en position " + Position.Name);
            }
        }

        private void DecreasePosition()
        {
            if (PantographSelectorPosition.Name != "0")
            {
                Message(CabControl.None, "Selecteur de tension verrouillé - Baissez le pantographe pour changer de tension");
                return;
            }

            int index = Positions.IndexOf(Position);

            if (index > 0)
            {
                index--;

                Position = Positions[index];

                SignalEvent(Event.VoltageSelectorDecrease);
                Message(CabControl.None, "Selecteur de tension en position " + Position.Name);
            }
        }
    }
}
