using Simulant.UI;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Simulant.Simulation.Options
{
    public class IntegerOption : SimOption<int>
    {
        private readonly int _min;
        private readonly int _max;

        public IntegerOption(string propName, string labelText, int defaultValue, int max = 100, int min = 0, string hint = null)
        {
            PropertyName = propName;
            _labelText = labelText;
            _default = defaultValue;
            _min = min;
            _max = max;
            _hint = hint;
        }

        protected override Control CreateControl(PresetControl presetControl)
        {
            var numericUpDown = new NumericUpDown
            {
                Minimum = _min,
                Maximum = _max,
                DecimalPlaces = 0,
                Value = _default,
            };
            presetControl.ApplyNumericUpDownStyle(numericUpDown);
            return numericUpDown;
        }

        internal override int GetValue() => (int?)(Control as NumericUpDown)?.Value ?? _default;
    }
}
