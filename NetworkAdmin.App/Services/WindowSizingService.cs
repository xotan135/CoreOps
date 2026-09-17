using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;

namespace NetworkAdmin.App.Services;

public static class WindowSizingService
{
    private const int ShowNormal = 1;
    private const int ShowMaximized = 3;
    private const uint MonitorDefaultToNearest = 2;
    private static readonly object FileLock = new();
    private static readonly string PlacementPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CoreOps", "window-placement.json");

    public static void RememberPlacement(Window window, string key)
    {
        window.SourceInitialized += (_, _) => Restore(window, key);
        window.Closing += (_, _) => Save(window, key);
    }

    private static void Restore(Window window, string key)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var placements = Load();
        if (placements.TryGetValue(key, out var saved))
        {
            var placement = NewPlacement();
            placement.ShowCommand = saved.Maximized ? ShowMaximized : ShowNormal;
            placement.NormalPosition = new NativeRect(saved.Left, saved.Top, saved.Right, saved.Bottom);
            ClampToWorkArea(handle, ref placement.NormalPosition);
            SetWindowPlacement(handle, ref placement);
            return;
        }

        var current = NewPlacement();
        if (!GetWindowPlacement(handle, ref current)) return;
        ClampToWorkArea(handle, ref current.NormalPosition);
        SetWindowPlacement(handle, ref current);
    }

    private static void Save(Window window, string key)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var placement = NewPlacement();
        if (!GetWindowPlacement(handle, ref placement)) return;
        var rect = placement.NormalPosition;
        if (rect.Right <= rect.Left || rect.Bottom <= rect.Top) return;

        lock (FileLock)
        {
            var placements = LoadUnsafe();
            placements[key] = new SavedPlacement
            {
                Left = rect.Left, Top = rect.Top, Right = rect.Right, Bottom = rect.Bottom,
                Maximized = placement.ShowCommand == ShowMaximized || window.WindowState == WindowState.Maximized
            };
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PlacementPath)!);
                File.WriteAllText(PlacementPath, JsonSerializer.Serialize(placements, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
    }

    private static Dictionary<string, SavedPlacement> Load()
    {
        lock (FileLock) return LoadUnsafe();
    }

    private static Dictionary<string, SavedPlacement> LoadUnsafe()
    {
        try
        {
            return File.Exists(PlacementPath)
                ? JsonSerializer.Deserialize<Dictionary<string, SavedPlacement>>(File.ReadAllText(PlacementPath)) ?? []
                : [];
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException) { return []; }
    }

    private static void ClampToWorkArea(IntPtr handle, ref NativeRect rect)
    {
        var width = Math.Max(320, rect.Right - rect.Left);
        var height = Math.Max(240, rect.Bottom - rect.Top);
        var monitor = MonitorFromRect(ref rect, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero) monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return;

        width = Math.Min(width, info.Work.Right - info.Work.Left);
        height = Math.Min(height, info.Work.Bottom - info.Work.Top);
        var left = Math.Clamp(rect.Left, info.Work.Left, info.Work.Right - width);
        var top = Math.Clamp(rect.Top, info.Work.Top, info.Work.Bottom - height);
        rect = new NativeRect(left, top, left + width, top + height);
    }

    private static WindowPlacement NewPlacement() => new() { Length = Marshal.SizeOf<WindowPlacement>() };

    private sealed class SavedPlacement
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }
        public bool Maximized { get; set; }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowPlacement(IntPtr handle, ref WindowPlacement placement);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPlacement(IntPtr handle, [In] ref WindowPlacement placement);
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect([In] ref NativeRect rect, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPlacement
    {
        public int Length;
        public int Flags;
        public int ShowCommand;
        public NativePoint MinPosition;
        public NativePoint MaxPosition;
        public NativeRect NormalPosition;
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor; public NativeRect Work; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public NativeRect(int left, int top, int right, int bottom) { Left=left; Top=top; Right=right; Bottom=bottom; }
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
