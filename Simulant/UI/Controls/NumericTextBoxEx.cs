using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Forms;

namespace Simulant.UI
{
    [DefaultProperty(nameof(Value))]
    [DefaultEvent(nameof(ValueChanged))]
    public class NumericTextBoxEx : TextBoxEx
    {
        private decimal _minimum;
        private decimal _maximum = 100;
        private int _decimalPlaces;
        private decimal _value;
        private string _editText = string.Empty;
        private string _valueText;
        private bool _revertingText;

        public NumericTextBoxEx()
        {
            BorderStyle = BorderStyle.None;
            TextAlign = HorizontalAlignment.Right;
            ImeMode = ImeMode.Disable;
            ShowValue();
        }

        [DefaultValue(typeof(decimal), "0")]
        public decimal Minimum
        {
            get => _minimum;
            set
            {
                _minimum = value;
                _maximum = Math.Max(_maximum, value);
                SetValue(_value);
            }
        }

        [DefaultValue(typeof(decimal), "100")]
        public decimal Maximum
        {
            get => _maximum;
            set
            {
                _maximum = value;
                _minimum = Math.Min(_minimum, value);
                SetValue(_value);
            }
        }

        [DefaultValue(0)]
        public int DecimalPlaces
        {
            get => _decimalPlaces;
            set
            {
                if (value < 0 || value > 28)
                    throw new ArgumentOutOfRangeException(nameof(value));

                _decimalPlaces = value;
                SetValue(_value);
            }
        }

        [DefaultValue(typeof(decimal), "0")]
        public decimal Value
        {
            get => _value;
            set
            {
                if (value < Minimum || value > Maximum)
                    throw new ArgumentOutOfRangeException(nameof(value));

                SetValue(value);
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override string Text
        {
            get => base.Text;
            set => base.Text = value;
        }

        public event EventHandler ValueChanged;

        protected override void OnTextChanged(EventArgs e)
        {
            if (_revertingText)
                return;

            if (!IsEditableNumber(Text))
            {
                var position = Math.Min(_editText.Length, SelectionStart);
                _revertingText = true;
                try
                {
                    Text = _editText;
                    Select(position, 0);
                }
                finally
                {
                    _revertingText = false;
                }

                return;
            }

            _editText = Text;
            base.OnTextChanged(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            CommitEdit();
            base.OnLostFocus(e);
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData != Keys.Enter)
                return base.ProcessDialogKey(keyData);

            CommitEdit();
            base.ProcessDialogKey(keyData);
            return true;
        }

        private void CommitEdit()
        {
            if (Text == _valueText)
                return;

            if (decimal.TryParse(Text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.CurrentCulture, out var value))
                SetValue(decimal.Round(value, DecimalPlaces, MidpointRounding.AwayFromZero));
            else
                ShowValue();
        }

        private void SetValue(decimal value)
        {
            var next = Math.Min(Maximum, Math.Max(Minimum, value));
            var changed = next != _value;
            _value = next;
            ShowValue();

            if (changed)
                ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ShowValue()
        {
            _valueText = _value.ToString("F" + DecimalPlaces, CultureInfo.CurrentCulture);
            Text = _valueText;
        }

        private bool IsEditableNumber(string text)
        {
            var format = CultureInfo.CurrentCulture.NumberFormat;
            var start = Minimum < 0 && text.StartsWith(format.NegativeSign, StringComparison.Ordinal)
                ? format.NegativeSign.Length
                : 0;
            var separator = format.NumberDecimalSeparator;
            var hasSeparator = false;

            for (var index = start; index < text.Length; index++)
            {
                if (text[index] >= '0' && text[index] <= '9')
                    continue;

                if (DecimalPlaces == 0 || hasSeparator ||
                    string.CompareOrdinal(text, index, separator, 0, separator.Length) != 0)
                    return false;

                hasSeparator = true;
                index += separator.Length - 1;
            }

            return true;
        }
    }
}
