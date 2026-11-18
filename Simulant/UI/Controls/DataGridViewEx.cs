using System.Drawing;
using System.Windows.Forms;

namespace Simulant.UI
{
    public class DataGridViewEx : DataGridView
    {
        public DataGridViewEx()
        {
            DoubleBuffered = true;
            BackgroundColor = WorkbenchTheme.Canvas;
            BorderStyle = BorderStyle.None;
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            GridColor = WorkbenchTheme.Surface;
            EnableHeadersVisualStyles = false;
            DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = WorkbenchTheme.Canvas,
                ForeColor = WorkbenchTheme.Text,
                SelectionBackColor = WorkbenchTheme.Selection,
                SelectionForeColor = WorkbenchTheme.Text,
                Padding = new Padding(9, 0, 4, 0)
            };
            AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = WorkbenchTheme.Alternate };
            var header = new DataGridViewCellStyle
            {
                BackColor = WorkbenchTheme.Field,
                ForeColor = WorkbenchTheme.Muted,
                SelectionBackColor = WorkbenchTheme.Field,
                SelectionForeColor = WorkbenchTheme.Text,
                Font = WorkbenchTheme.SmallFont,
                Padding = new Padding(9, 0, 0, 0)
            };
            ColumnHeadersDefaultCellStyle = header;
            RowHeadersDefaultCellStyle = new DataGridViewCellStyle(header);
            ScrollBarTheme.Attach(VerticalScrollBar);
            ScrollBarTheme.Attach(HorizontalScrollBar);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!VerticalScrollBar.Visible || !HorizontalScrollBar.Visible)
                return;

            using (var fill = new SolidBrush(WorkbenchTheme.Canvas))
                e.Graphics.FillRectangle(fill, VerticalScrollBar.Left, HorizontalScrollBar.Top,
                    VerticalScrollBar.Width, HorizontalScrollBar.Height);
        }
    }
}
