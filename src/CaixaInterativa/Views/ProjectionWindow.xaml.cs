// Caixa de Areia Interativa — sistema de projeção topográfica interativa
// Copyright (C) 2026 Projeto Caixa de Areia
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
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Threading;
using CaixaInterativa.Config;
using CaixaInterativa.Simulation;

namespace CaixaInterativa.Views;

public partial class ProjectionWindow : Window
{
    private readonly SandboxEngine _engine;

    public event Action? SaveRequested;
    public event Action? CalibrateRequested;

    private readonly DispatcherTimer _dadosTimer;

    public ProjectionWindow(SandboxEngine engine)
    {
        InitializeComponent();
        _engine = engine;

        // Meio segundo é o bastante: números que piscam a 30 Hz não se leem de longe,
        // e a turma precisa acompanhar a tendência, não o valor instantâneo.
        _dadosTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _dadosTimer.Tick += (_, _) => AtualizarDados();
        _dadosTimer.Start();

        _engine.BitmapReplaced += AttachBitmap;
        Loaded += OnLoaded;
        Closed += (_, _) => { _engine.BitmapReplaced -= AttachBitmap; _dadosTimer.Stop(); };
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

            case Key.D:
                PainelDados.Visibility = PainelDados.Visibility == Visibility.Visible
                    ? Visibility.Collapsed : Visibility.Visible;
                AtualizarDados();
                break;

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

    /// <summary>
    /// Monta as linhas de resultado da simulação que estiver rodando.
    ///
    /// Tipografia grande e poucos números: isto é lido a três metros de distância, por
    /// uma sala inteira, e não é um painel de controle.
    /// </summary>
    private void AtualizarDados()
    {
        if (PainelDados.Visibility != Visibility.Visible) return;

        var agua = _engine.Agua;
        var sismo = _engine.Terremoto;
        var fogo = _engine.Fogo;

        DadosLinhas.Children.Clear();

        if (fogo is { Ativo: true } && (fogo.EmAndamento || fogo.AreaQueimadaPercent > 0))
        {
            DadosTitulo.Text = "QUEIMADA";
            Linha("Área queimada", $"{fogo.AreaQueimadaPercent:F0}%", "#FFE07A4A");
            if (fogo.EmAndamento)
            {
                Linha("Em chamas agora", $"{fogo.AreaEmChamasPercent:F1}%", "#FFFFC048");
                DadosRodape.Text = $"Vento de {fogo.VentoPorExtenso()} · {fogo.TempoDecorrido:F0}s";
            }
            else
            {
                DadosRodape.Text = "O fogo apagou. O solo queimado repele a água.";
            }
            return;
        }

        if (sismo is { Ativo: true } && (sismo.EmAndamento || sismo.AreaAfetadaPercent > 0))
        {
            DadosTitulo.Text = "TERREMOTO";
            Linha("Magnitude", $"{sismo.Magnitude:F1}", "#FFFFC048");
            Linha("Área afetada", $"{sismo.AreaAfetadaPercent:F0}%", "#FFE07A4A");
            Linha("Risco de deslizamento", $"{sismo.AreaDeslizamentoPercent:F1}%", "#FFE06A55");
            DadosRodape.Text = sismo.EmAndamento
                ? $"Tremendo há {sismo.TempoDecorrido:F0}s"
                : "O tremor passou. O mapa mostra onde bateu mais forte.";
            return;
        }

        if (agua is { Ativo: true })
        {
            DadosTitulo.Text = agua.Chovendo ? "CHOVENDO" : "ENCHENTE";

            Linha("Área alagada", $"{agua.AreaAlagadaPercent:F0}%", "#FF66B8E8");
            if (agua.PicoAlagamentoPercent > agua.AreaAlagadaPercent + 0.5)
                Linha("Pico do episódio", $"{agua.PicoAlagamentoPercent:F0}%", "#FF4A93C8");

            Linha("Água na superfície", Litros(agua.VolumeLitros), "#FF7FCFE8");
            Linha("Absorvida pelo solo", Litros(agua.InfiltradoLitros), "#FF7CC7A2");
            Linha("Solo encharcado", $"{agua.SaturacaoMediaPercent:F0}%", "#FFDAB463");

            DadosRodape.Text = agua.Chovendo
                ? $"Faltam {agua.ChuvaRestanteSegundos:F0}s de chuva"
                : agua.SaturacaoMediaPercent > 80
                    ? "O solo está cheio: qualquer chuva agora vira enxurrada."
                    : "A chuva parou. A água escoa e o solo absorve o que consegue.";
            return;
        }

        DadosTitulo.Text = "CAIXA DE AREIA";
        DadosRodape.Text = "Nenhuma simulação em andamento.";
    }

    /// <summary>
    /// Volume em litros, com a marca de estimativa enquanto a largura coberta pelo sensor
    /// não for medida. Aqui não cabe a explicação longa — o painel é lido de três metros
    /// —, mas o sinal impede que a turma leia o número como medição exata.
    /// </summary>
    private string Litros(double valor)
        => _engine.Config.Caixa.LarguraMedida ? $"{valor:F1} L" : $"≈ {valor:F1} L";

    /// <summary>Uma linha de resultado: rótulo pequeno, número grande.</summary>
    private void Linha(string rotulo, string valor, string cor)
    {
        var painel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

        painel.Children.Add(new TextBlock
        {
            Text = rotulo.ToUpperInvariant(),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0xA6, 0xAE)),
            Margin = new Thickness(0, 0, 0, 1),
        });

        painel.Children.Add(new TextBlock
        {
            Text = valor,
            FontSize = 34,
            FontWeight = FontWeights.Bold,
            Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom(cor)!,
        });

        DadosLinhas.Children.Add(painel);
    }

    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_NOZORDER = 0x0004;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
}
