using System.ComponentModel;
using System.Windows.Forms;

namespace Simulant.UI
{
    public enum LabelTone
    {
        Body,
        Small,
        Muted,
        Caption,
        Heading,
        Title,
        PresetTitle
    }

    public class LabelEx : Label
    {
        private LabelTone _tone;

        [DefaultValue(LabelTone.Body)]
        public LabelTone Tone
        {
            get => _tone;
            set
            {
                if (_tone == value)
                    return;

                _tone = value;
                Font = value == LabelTone.Title ? WorkbenchTheme.TitleFont :
                    value == LabelTone.PresetTitle ? WorkbenchTheme.PresetTitleFont :
                    value == LabelTone.Heading ? WorkbenchTheme.SectionFont :
                    value == LabelTone.Body ? null : WorkbenchTheme.SmallFont;
                ForeColor = value == LabelTone.Muted ? WorkbenchTheme.Muted :
                    value == LabelTone.Caption ? WorkbenchTheme.Dim : WorkbenchTheme.Text;
            }
        }
    }
}
