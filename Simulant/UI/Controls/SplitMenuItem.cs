using System;
using System.Drawing;
using System.Windows.Forms;

namespace Simulant.UI
{
    internal sealed class SplitMenuItem : ToolStripMenuItem
    {
        private bool _pressedDefault;

        internal Action DefaultAction { get; set; }
        internal Rectangle ArrowBounds { get; set; }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _pressedDefault = e.Button == MouseButtons.Left && !HitArrow(e.X);
            if (!_pressedDefault)
                base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!_pressedDefault)
            {
                base.OnMouseUp(e);
                return;
            }

            _pressedDefault = false;
            if (e.Button == MouseButtons.Left && new Rectangle(Point.Empty, Size).Contains(e.Location) && !HitArrow(e.X))
                InvokeDefault();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _pressedDefault = false;
            base.OnMouseLeave(e);
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData != Keys.Enter || DefaultAction == null || !Enabled)
                return base.ProcessDialogKey(keyData);

            InvokeDefault();
            return true;
        }

        private bool HitArrow(int x) => !ArrowBounds.IsEmpty &&
            (RightToLeft == RightToLeft.Yes ? x < ArrowBounds.Right : x >= ArrowBounds.Left);

        private void InvokeDefault()
        {
            var action = DefaultAction;
            if (!Enabled || action == null)
                return;

            HideDropDown();
            (Owner as ToolStripDropDown)?.Close(ToolStripDropDownCloseReason.ItemClicked);
            action();
        }
    }
}
