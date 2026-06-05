using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SqlQueryGenerator.App;
using SqlQueryGenerator.App.ViewModels;

namespace LayoutPreviewRunner;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string outputPath = args.Length > 0
            ? Path.GetFullPath(args[0])
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "artifacts", "layout-preview.png"));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        if (Application.ResourceAssembly is null)
        {
            Application.ResourceAssembly = typeof(MainWindow).Assembly;
        }

        Application application = new();
        MainWindow window = new()
        {
            WindowState = WindowState.Normal,
            Left = 0,
            Top = 0,
            Width = 1920,
            Height = 1080,
            ShowInTaskbar = false
        };

        MainViewModel viewModel = (MainViewModel)window.DataContext;
        PopulateSuccessPreview(viewModel);

        application.MainWindow = window;
        window.Show();
        window.Activate();
        window.Focus();
        window.UpdateLayout();

        PumpUi(TimeSpan.FromMilliseconds(1400));
        CaptureWindow(window, outputPath);
        CaptureReverseDiagnosticsTab(window, outputPath);
        CaptureScaledPreview(window, outputPath, 1.25);
        CaptureCompactPreview(window, outputPath);
        CaptureFailurePreview(window, viewModel, outputPath);

        window.Close();
        application.Shutdown();

        Console.WriteLine($"Layout preview saved to {outputPath}");
        return 0;
    }

    private static void PopulateSuccessPreview(MainViewModel vm)
    {
        const string schema = """
            CREATE TABLE CUSTOMER (
                ID INTEGER PRIMARY KEY,
                NAME TEXT,
                SEGMENT TEXT
            );

            CREATE TABLE ORDERS (
                ORDER_ID INTEGER PRIMARY KEY,
                CUSTOMER_ID INTEGER NOT NULL,
                STATUS TEXT,
                TOTAL_AMOUNT DECIMAL(10,2),
                CONSTRAINT FK_ORDERS_CUSTOMER FOREIGN KEY (CUSTOMER_ID) REFERENCES CUSTOMER(ID)
            );
            """;

        const string rawSql = """
            -- Legacy reporting query kept for layout verification
            SELECT
                CUSTOMER.ID,
                CUSTOMER.NAME,
                COUNT(ORDERS.ORDER_ID) AS ORDER_COUNT
            FROM CUSTOMER
            LEFT JOIN ORDERS ON CUSTOMER.ID = ORDERS.CUSTOMER_ID
            WHERE CUSTOMER.NAME LIKE '%Corp%'
            GROUP BY CUSTOMER.ID, CUSTOMER.NAME
            ORDER BY CUSTOMER.NAME
            """;

        vm.LoadSchemaFromText(schema, "layout-preview");
        vm.RawSqlText = rawSql;
        vm.QueryName = "layout_preview";
        vm.QueryDescription = "Prévisualisation ergonomie 1080p";
        vm.ReverseEngineerRawSqlCommand.Execute(null);
    }

    private static void PumpUi(TimeSpan duration)
    {
        DispatcherFrame frame = new();
        DispatcherTimer timer = new()
        {
            Interval = duration
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void CaptureWindow(Window window, string outputPath)
    {
        double width = Math.Ceiling(window.ActualWidth);
        double height = Math.Ceiling(window.ActualHeight);
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("Unable to capture rendered window size.");
        }

        RenderTargetBitmap bitmap = new(
            (int)width,
            (int)height,
            96,
            96,
            PixelFormats.Pbgra32);

        DrawingVisual visual = new();
        using (DrawingContext context = visual.RenderOpen())
        {
            context.DrawRectangle(new VisualBrush(window), null, new Rect(new Point(), new Size(width, height)));
        }

        bitmap.Render(visual);

        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(outputPath);
        encoder.Save(stream);
    }

    private static void CaptureReverseDiagnosticsTab(Window window, string outputPath)
    {
        TabControl? diagnosticsTabs = FindDiagnosticsTabControl(window);
        if (diagnosticsTabs is null)
        {
            return;
        }

        if (!SelectDiagnosticsTab(diagnosticsTabs, "Reverse SQL"))
        {
            return;
        }

        window.UpdateLayout();
        PumpUi(TimeSpan.FromMilliseconds(500));

        string reverseOutputPath = Path.Combine(
            Path.GetDirectoryName(outputPath)!,
            Path.GetFileNameWithoutExtension(outputPath) + "-reverse" + Path.GetExtension(outputPath));
        CaptureWindow(window, reverseOutputPath);
        Console.WriteLine($"Reverse diagnostics preview saved to {reverseOutputPath}");
    }

    private static void CaptureCompactPreview(Window window, string outputPath)
    {
        TabControl? diagnosticsTabs = FindDiagnosticsTabControl(window);
        if (diagnosticsTabs is not null)
        {
            SelectDiagnosticsTab(diagnosticsTabs, "But");
        }

        window.Width = 1536;
        window.Height = 864;
        window.UpdateLayout();
        PumpUi(TimeSpan.FromMilliseconds(600));

        string compactOutputPath = Path.Combine(
            Path.GetDirectoryName(outputPath)!,
            Path.GetFileNameWithoutExtension(outputPath) + "-compact" + Path.GetExtension(outputPath));
        CaptureWindow(window, compactOutputPath);
        Console.WriteLine($"Compact layout preview saved to {compactOutputPath}");
    }

    private static void CaptureScaledPreview(Window window, string outputPath, double scale)
    {
        if (window.Content is not FrameworkElement rootContent)
        {
            return;
        }

        TabControl? diagnosticsTabs = FindDiagnosticsTabControl(window);
        if (diagnosticsTabs is not null)
        {
            SelectDiagnosticsTab(diagnosticsTabs, "But");
        }

        Transform originalTransform = rootContent.LayoutTransform;
        rootContent.LayoutTransform = new ScaleTransform(scale, scale);
        window.UpdateLayout();
        PumpUi(TimeSpan.FromMilliseconds(700));

        string scaleLabel = scale.ToString("0.##", CultureInfo.InvariantCulture).Replace(".", string.Empty);
        string scaledOutputPath = Path.Combine(
            Path.GetDirectoryName(outputPath)!,
            Path.GetFileNameWithoutExtension(outputPath) + $"-scale{scaleLabel}" + Path.GetExtension(outputPath));
        CaptureWindow(window, scaledOutputPath);
        Console.WriteLine($"Scaled layout preview saved to {scaledOutputPath}");

        rootContent.LayoutTransform = originalTransform;
        window.UpdateLayout();
        PumpUi(TimeSpan.FromMilliseconds(300));
    }

    private static void CaptureFailurePreview(Window window, MainViewModel viewModel, string outputPath)
    {
        window.Width = 1920;
        window.Height = 1080;
        window.UpdateLayout();
        PumpUi(TimeSpan.FromMilliseconds(300));

        SelectAnyTab(window, "Rétro-ingénierie SQL");
        if (FindDiagnosticsTabControl(window) is TabControl diagnosticsTabs)
        {
            SelectDiagnosticsTab(diagnosticsTabs, "Reverse SQL");
        }

        viewModel.RawSqlText = """
            SELECT
                c.CUSTOMER_ID
            FROM CUSTOMER c
            WHERE
            ORDER BY c.CUSTOMER_ID
            """;
        viewModel.ReverseEngineerRawSqlCommand.Execute(null);

        window.UpdateLayout();
        PumpUi(TimeSpan.FromMilliseconds(700));

        string failureOutputPath = Path.Combine(
            Path.GetDirectoryName(outputPath)!,
            Path.GetFileNameWithoutExtension(outputPath) + "-failure" + Path.GetExtension(outputPath));
        CaptureWindow(window, failureOutputPath);
        Console.WriteLine($"Reverse failure preview saved to {failureOutputPath}");
    }

    private static TabControl? FindDiagnosticsTabControl(DependencyObject root)
    {
        return FindVisualChildren<TabControl>(root)
            .FirstOrDefault(control => control.Items.OfType<TabItem>().Any(item => string.Equals(item.Header?.ToString(), "Reverse SQL", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool SelectDiagnosticsTab(TabControl diagnosticsTabs, string header)
    {
        TabItem? tab = diagnosticsTabs.Items.OfType<TabItem>()
            .FirstOrDefault(item => string.Equals(item.Header?.ToString(), header, StringComparison.OrdinalIgnoreCase));
        if (tab is null)
        {
            return false;
        }

        diagnosticsTabs.SelectedItem = tab;
        return true;
    }

    private static bool SelectAnyTab(DependencyObject root, string header)
    {
        foreach (TabControl control in FindVisualChildren<TabControl>(root))
        {
            if (SelectDiagnosticsTab(control, header))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        int childrenCount = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < childrenCount; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (T descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
