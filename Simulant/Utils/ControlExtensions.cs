using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Simulant.Utils
{
    public static class ControlExtensions
    {
        public static object SafeInvoke(this Control control, Delegate method)
        {
            var asyncResult = control.BeginInvoke(method);

            return SafeWait(control, asyncResult);
        }

        public static object SafeInvoke(this Control control, Delegate method, object[] args)
        {
            var asyncResult = control.BeginInvoke(method, args);

            return SafeWait(control, asyncResult);
        }

        private static object SafeWait(Control control, IAsyncResult result)
        {
            if (result.CompletedSynchronously)
            {
                return control.EndInvoke(result);
            }

            for (var i = 0; i < 50; i++)
            {
                if (control.IsDisposed || control.Disposing || !control.IsHandleCreated)
                {
                    break;
                }

                result.AsyncWaitHandle.WaitOne(100);

                if (result.IsCompleted)
                {
                    return control.EndInvoke(result);
                }
            }

            return null;
        }

        public static void AppendDateTimeLine(this RichTextBox target, string text)
        {
            if (target.InvokeRequired)
            {
                target.SafeInvoke(new Action(delegate
                {
                    target.AppendDateTimeLine(text);
                }));
            }
            else
            {
                target.AppendText($"\n[{DateTime.Now.ToLongTimeString()}] {text}");
                target.ScrollToCaret();
            }
        }
    }
}
