using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using static Simulant.UI.WorkbenchTheme;

namespace Simulant.UI
{
    public class ContextMenuStripEx : ContextMenuStrip
    {
        public ContextMenuStripEx()
        {
            Font = BodyFont;
            Renderer = new MenuRenderer();
        }

        public ContextMenuStripEx(IContainer container) : this()
        {
            container.Add(this);
        }

        private sealed class MenuRenderer : ToolStripProfessionalRenderer
        {
            internal MenuRenderer() : base(new MenuColors()) { }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Enabled ? WorkbenchTheme.Text : Dim;
                base.OnRenderItemText(e);
            }
        }

        private sealed class MenuColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Surface;
            public override Color ImageMarginGradientBegin => Surface;
            public override Color ImageMarginGradientMiddle => Surface;
            public override Color ImageMarginGradientEnd => Surface;
            public override Color MenuBorder => Stroke;
            public override Color MenuItemBorder => Hover;
            public override Color MenuItemSelected => Hover;
            public override Color SeparatorDark => Stroke;
            public override Color SeparatorLight => Stroke;
        }
    }
}
