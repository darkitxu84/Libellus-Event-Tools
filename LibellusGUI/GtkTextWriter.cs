using Gtk;
using System.IO;
using System.Text;

namespace LibellusGUI;

public class GtkTextWriter(TextView textView) : TextWriter
{
    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(string? value)
    {
        if (value is null) return;
        
        Application.Invoke((_, _) =>
        {
            var buffer = textView.Buffer;
            var bufferEndIter = buffer.EndIter;
            buffer.Insert(ref bufferEndIter, value);
            var mark = buffer.CreateMark(null, bufferEndIter, false);
            textView.ScrollToMark(mark, 0, false, 0, 0);
            buffer.DeleteMark(mark);
        });
    }

    public override void WriteLine(string? value) => Write((value ?? "") + "\n");
}