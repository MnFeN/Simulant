using Simulant.UI;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Simulant.Simulation.Options
{
    // No logic, just a label
    public class TextBoxOption : SimOption<string>
    {
        public TextBoxOption(string propName, string labelText, string defaultValue, string hint = null)
        {
            PropertyName = propName;
            _labelText = labelText;
            _default = defaultValue;
            _hint = hint;
        }

        protected override Control CreateControl(OptionEditor editor)
        {
            var txt = new TextBox
            {
                Text = _default,
            };
            editor.ApplyStyle(txt);
            return txt;
        }

        internal override string GetValue() => (Control as TextBox)?.Text ?? _default;
    }
}
