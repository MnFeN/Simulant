using Simulant.UI;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Simulant.Simulation.Options
{
    public class CheckBoxOption : SimOption<bool>
    {
        public CheckBoxOption(string propName, string labelText, bool defaultValue, string hint = null)
        {
            PropertyName = propName;
            _labelText = labelText;
            _default = defaultValue;
            _hint = hint;
        }

        protected override Control CreateControl(OptionEditor editor)
        {
            var chk = new CheckBoxEx();
            chk.Checked = _default;
            chk.AccessibleName = _labelText;
            editor.ApplyStyle(chk);
            return chk;
        }

        internal override bool GetValue() => (Control as CheckBox)?.Checked ?? _default;
        protected override void SetValue(bool value) => ((CheckBox)Control).Checked = value;
    }
}
