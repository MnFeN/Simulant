using Simulant.UI;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Simulant.Simulation.Options
{
    public class DecimalOption : SimOption<double>
    {
        private double _min;
        private double _max;
        private int _decimalPlaces;

        public DecimalOption(string propName, string labelText, double defaultValue, double max = 100, double min = 0, int decimalPlaces = 1, string hint = null)
        {
            PropertyName = propName;
            _labelText = labelText;
            _default = defaultValue;
            _min = min;
            _max = max;
            _decimalPlaces = decimalPlaces;
            _hint = hint;
        }

        protected override Control CreateControl(PresetControl presetControl)
        {
            var numericUpDown = new NumericUpDown
            {
                Minimum = (decimal)_min,
                Maximum = (decimal)_max,
                DecimalPlaces = _decimalPlaces,
                Value = (decimal)_default,
            }; 
            presetControl.ApplyNumericUpDownStyle(numericUpDown);
            return numericUpDown;
        }

        internal override double GetValue() => (double?)(Control as NumericUpDown)?.Value ?? _default;
    }
}
