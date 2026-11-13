using Simulant.UI;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Simulant.Simulation.Options
{
    public class LabelOption : SimOptionBase
    {
        public LabelOption(string labelText, string hint = null)
        {
            _labelText = labelText;
            _hint = hint;
        }

        protected override Control CreateControl(OptionEditor editor)
        {
            return null; // No control, just a label
        }

    }
}
