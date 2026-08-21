// Caixa de Areia Interativa — sistema de projeção topográfica interativa
// Copyright (C) 2026 Luis Filipe Gomes de Carvalho
//
// Este programa é software livre: você pode redistribuí-lo e/ou modificá-lo
// sob os termos da Licença Pública Geral GNU, conforme publicada pela Free
// Software Foundation, na versão 2 da Licença ou (a seu critério) qualquer
// versão posterior.
//
// Este programa é distribuído na esperança de que seja útil, mas SEM QUALQUER
// GARANTIA; sem sequer a garantia implícita de COMERCIALIZAÇÃO ou ADEQUAÇÃO A
// UMA FINALIDADE ESPECÍFICA. Consulte a Licença Pública Geral GNU para mais
// detalhes. Uma cópia acompanha este programa no arquivo LICENSE.

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using CaixaInterativa.Config;

namespace CaixaInterativa.Views;

public partial class ProjectionWindow : Window
{
    private readonly SandboxEngine _engine;

    public event Action? SaveRequested;
    public event Action? CalibrateRequested;

    public ProjectionWindow(SandboxEngine engine)
    {
        InitializeComponent();
        _engine = engine;

        _engine.BitmapReplaced += AttachBitmap;
        Loaded += OnLoaded;
        Closed += (_, _) => _engine.BitmapReplaced -= AttachBitmap;
        SizeChanged += (_, _) => BuildAlignmentGrid();
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        AttachBitmap();
        ApplyTransform();
        BuildAlignmentGrid();
        MoveToScreen(_engine.Config.Projection.ScreenIndex);
        Focus();
    }

    private void AttachBitmap() => Projected.Source = _engine.Bitmap;

    /// <summary>
    /// Posiciona a janela via Win32 em pixels de dispositivo.
    /// Left/Top do WPF sao unidades independentes de DPI, e com o notebook em 125%
    /// e o projetor em 100% a conversao entre monitores nao e' confiavel - a janela
    /// acabaria deslocada ou menor que a tela. SetWindowPos nao tem essa ambiguidade.
    /// </summary>
    public void MoveToScreen(int index)
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        if (screens.Length == 0) return;

        var screen = screens[Math.Clamp(index, 0, screens.Length - 1)];
        var b = screen.Bounds;

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        SetWindowPos(hwnd, IntPtr.Zero, b.X, b.Y, b.Width, b.Height, SWP_SHOWWINDOW | SWP_NOZORDER);
        BuildAlignmentGrid();
    }

    public void ApplyTransform()
    {
        var p = _engine.Config.Projection;
        Scale.ScaleX = p.ScaleX * (p.FlipHorizontal ? -1 : 1);
        Scale.ScaleY = p.ScaleY * (p.FlipVertical ? -1 : 1);
        Rotate.Angle = p.RotationDegrees;
        Translate.X = p.OffsetX;
        Translate.Y = p.OffsetY;
    }

    private void BuildAlignmentGrid()
    {
        AlignmentGrid.Children.Clear();
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        var thin = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));
        var thick = new SolidColorBrush(Color.FromArgb(220, 255, 60, 60));

        const int divisions = 10;
        for (int i = 0; i <= divisions; i++)
        {
            double fx = w * i / divisions;
            double fy = h * i / divisions;
            bool edge = i == 0 || i == divisions;

            AlignmentGrid.Children.Add(new Line
            {
                X1 = fx, Y1 = 0, X2 = fx, Y2 = h,
                Stroke = edge ? thick : thin,
                StrokeThickness = edge ? 3 : 1
            });
            AlignmentGrid.Children.Add(new Line
            {
                X1 = 0, Y1 = fy, X2 = w, Y2 = fy,
                Stroke = edge ? thick : thin,
                StrokeThickness = edge ? 3 : 1
            });
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var p = _engine.Config.Projection;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        double step = shift ? 10 : 1;
        double scaleStep = shift ? 0.05 : 0.005;

        switch (e.Key)
        {
            case Key.Left:
                if (ctrl) p.ScaleX -= scaleStep; else p.OffsetX -= step;
                break;
            case Key.Right:
                if (ctrl) p.ScaleX += scaleStep; else p.OffsetX += step;
                break;
            case Key.Up:
                if (ctrl) p.ScaleY += scaleStep; else p.OffsetY -= step;
                break;
            case Key.Down:
                if (ctrl) p.ScaleY -= scaleStep; else p.OffsetY += step;
                break;

            case Key.OemPlus or Key.Add:
                p.ScaleX += scaleStep; p.ScaleY += scaleStep;
                break;
            case Key.OemMinus or Key.Subtract:
                p.ScaleX -= scaleStep; p.ScaleY -= scaleStep;
                break;

            case Key.R: p.RotationDegrees += shift ? 1.0 : 0.1; break;
            case Key.E: p.RotationDegrees -= shift ? 1.0 : 0.1; break;

            case Key.H: p.FlipHorizontal = !p.FlipHorizontal; break;
            case Key.V: p.FlipVertical = !p.FlipVertical; break;

            case Key.G:
                AlignmentGrid.Visibility = AlignmentGrid.Visibility == Visibility.Visible
                    ? Visibility.Collapsed : Visibility.Visible;
                break;

            case Key.F1:
                HelpPanel.Visibility = HelpPanel.Visibility == Visibility.Visible
                    ? Visibility.Collapsed : Visibility.Visible;
                break;

            case Key.C: CalibrateRequested?.Invoke(); break;
            case Key.S: SaveRequested?.Invoke(); break;

            case Key.Escape: Close(); break;

            default:
                base.OnKeyDown(e);
                return;
        }

        ApplyTransform();
        e.Handled = true;
    }

    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_NOZORDER = 0x0004;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
}
