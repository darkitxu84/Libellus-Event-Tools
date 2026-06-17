using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Gdk;
using Gtk;
using LibellusLibrary.Event;
using Window = Gtk.Window;

namespace LibellusGUI;

static class Program
{
    private const string AppName = "Libellus GUI";
    private const string DefaultDropBoxLabel = "Drop the folder/file here";

    private static bool _recursive = false;
    private static bool _running = false;
    
    private static async Task ProcessPaths(string[] paths)
    {
        foreach (string path in paths)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                Console.WriteLine($"'{path}' is not a valid file or directory!");
                continue;
            }
				
            string ext = Path.GetExtension(path).ToLower();
            if (ext is ".pm1" or ".pm2" or ".pm3")
            {
                Console.WriteLine($"Converting to Json: {path}");
                PmdReader reader = new();
                PolyMovieData pmd = await reader.ReadPmd(path);
                string folder = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path));
                await pmd.ExtractPmd(folder, Path.GetFileName(path));
            }
            else if (ext == ".json")
            {
                Console.WriteLine($"Converting to PMD: {path}");
                PolyMovieData pmd = new PolyMovieData();
                try
                {
                    pmd = await PolyMovieData.LoadPmd(path);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Cannot convert '{path}' to PMD!");
                    Console.WriteLine($"{e.GetType().ToString()}: {e.Message}");
                    continue;
                }

                pmd.SavePmd($"{path}.PM{pmd.MagicCode[3]}");
            }
            else if (Directory.Exists(path))
            {
                var searchOption = _recursive 
                    ? SearchOption.AllDirectories 
                    : SearchOption.TopDirectoryOnly;
                await ProcessPaths(Directory.GetFiles(path, "*", searchOption));
            }
        }
    }
    
    [STAThread]
    public static void Main(string[] args)
    {
        Application.Init();

        var win = new Window(AppName) { Resizable = false };
        var assembly = Assembly.GetExecutingAssembly();
        using (var stream = assembly.GetManifestResourceStream("LibellusGUI.Resources.heeho.png"))
        {
            if (stream is not null)
            {
                var pixBuf = new Gdk.Pixbuf(stream);
                win.Icon = pixBuf;
            }
        }
        win.SetDefaultSize(600, 400);
        win.DeleteEvent += (o, args) =>
        {
            if (!_running) Application.Quit();
            
            args.RetVal = true;
            
            var dialog = new MessageDialog(
                win, 
                DialogFlags.Modal, 
                MessageType.Warning,
                ButtonsType.YesNo,
                "Are you sure you want to quit the application? LEET is processing");
            
            dialog.Response += (_, resp) =>
            {
                if (resp.ResponseId == ResponseType.Yes)
                    Application.Quit();

                dialog.Destroy();
            };
            
            dialog.Show();
        };
        
        var box = new Box(Orientation.Vertical, 10);
        box.MarginTop = box.MarginEnd = box.MarginStart = box.MarginBottom = 20;

        var toggleButton = new ToggleButton("Recursive: Disabled");
        toggleButton.Toggled += (o, args) =>
        {
            _recursive = toggleButton.Active;
            toggleButton.Label = _recursive ? "Recursive: Enabled" : "Recursive: Disabled";
        };
        
        var dropFrame = new Frame() { ShadowType = ShadowType.EtchedIn };
        var eventBox = new EventBox() { HeightRequest = 150 };
        var dropLabel = new Label(DefaultDropBoxLabel);
        eventBox.Add(dropLabel);
        dropFrame.Add(eventBox);

        var targets = new TargetEntry[] { new TargetEntry("text/uri-list", 0, 0) };
        Gtk.Drag.DestSet(eventBox, Gtk.DestDefaults.All, targets, Gdk.DragAction.Copy);

        var textView = new TextView()
        {
            Editable = false,
            WrapMode = WrapMode.Word,
            Monospace = true,
            CursorVisible = false
        };
        var scroll = new ScrolledWindow();
        scroll.Add(textView);
        scroll.HeightRequest = 220;

        var gtkWriter = new GtkTextWriter(textView);
        Console.SetOut(gtkWriter);
        Console.SetError(gtkWriter);

        eventBox.DragDataReceived += async (o, args) =>
        {
            var uris = args.SelectionData.Uris;
            if (uris == null || uris.Length == 0) return;

            // Convert the URIs to paths
            var paths = uris
                .Select(x => new Uri(x).LocalPath)
                .ToArray();
            
            // disable the ui
            eventBox.Sensitive = false;
            toggleButton.Sensitive = false;
            dropLabel.Text = "Processing...";
            _running = true;
            
            await Task.Run(() => ProcessPaths(paths));
            
            eventBox.Sensitive = true;
            toggleButton.Sensitive = true;
            dropLabel.Text = DefaultDropBoxLabel;
            _running = false;
        };

        box.PackStart(toggleButton, false, false, 0);
        box.PackStart(dropFrame, false, false, 0);
        box.PackStart(scroll, true, true, 0);
        win.Add(box);
        win.ShowAll();
        Application.Run();
    }
}