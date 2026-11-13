using Simulant.UI;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Simulant.Simulation.Options
{
    public class ComboBoxOption<T> : SimOption<T>
    {
        private readonly Map<T> _options;
        public ComboBoxOption(string propName, string labelText, T defaultValue, Map<T> options, string hint = null)
        {
            _options = options;
            PropertyName = propName;
            _labelText = labelText;
            _default = defaultValue;
            _hint = hint;
        }

        protected override Control CreateControl(OptionEditor editor)
        {
            var comboBox = new ComboBox();
            editor.ApplyStyle(comboBox);

            comboBox.Items.AddRange(_options.Values.Select(s => $" {s}").ToArray());

            if (_options.TryGetIndex((T)_default, out int index))
                comboBox.SelectedIndex = index;
            else 
                throw new KeyNotFoundException("Default value not found in options: " + _default);

            return comboBox;
        }

        internal override T GetValue()
        {
            if (Control is ComboBox comboBox && _options.TryGetKeyAt(comboBox.SelectedIndex, out T key))
            {
                return key;
            }
            return _default;
        }
    }
}
