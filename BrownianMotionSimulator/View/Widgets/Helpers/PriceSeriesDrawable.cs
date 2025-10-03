
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Graphics;

namespace BrownianMotionSimulator.View.Widgets.Helpers
{
    /// <summary>
    /// Estilos suportados para o traçado das linhas (contínua, tracejada, pontilhada).
    /// </summary>
    public enum LineStyleOption { Solid, Dashed, Dotted }

    /// <summary>
    /// Drawable responsável por renderizar um gráfico de linhas com múltiplas séries de valores,
    /// exibindo eixos, grade (opcional) e legenda (opcional).
    /// </summary>
    public class PriceSeriesDrawable : IDrawable
    {
        /// <summary>
        /// Coleção de séries a serem desenhadas. Cada item representa uma linha (trajetória).
        /// A posição no índice corresponde ao “dia”/passo; o valor é o preço naquele passo.
        /// </summary>
        public IList<IReadOnlyList<double>> Series { get; set; } = new List<IReadOnlyList<double>>();

        /// <summary>
        /// Paleta de cores usada para as séries. Se a quantidade de séries exceder a de cores,
        /// as cores são reutilizadas em ciclo.
        /// </summary>
        public IList<Color> SeriesColors { get; set; } = new List<Color>();

        /// <summary>
        /// Exibe linhas de grade entre os ticks (X e Y) para facilitar a leitura visual.
        /// </summary>
        public bool ShowGrid { get; set; } = true;

        /// <summary>
        /// Exibe a legenda no canto superior direito do retângulo de plotagem.
        /// </summary>
        public bool ShowLegend { get; set; } = true;

        /// <summary>
        /// Define se os valores do eixo Y serão formatados como moeda (<c>"C2"</c>) ou numérico (<c>"N2"</c>).
        /// </summary>
        public bool UseCurrencyFormat { get; set; } = false;

        /// <summary>
        /// Espessura (em pixels) do traço das linhas do gráfico.
        /// </summary>
        public float StrokeSize { get; set; } = 2f;

        /// <summary>
        /// Estilo do traço das linhas (sólida/tracejada/pontilhada).
        /// </summary>
        public LineStyleOption LineStyle { get; set; } = LineStyleOption.Solid;

        // ---- Margens internas reservadas para rótulos, ticks e conforto visual ----
        private const float LeftMargin = 64f;
        private const float RightMargin = 20f;
        private const float TopMargin = 20f;
        private const float BottomMargin = 44f;

        /// <summary>
        /// Método principal de desenho do drawable. Renderiza eixo X/Y, grid (se habilitada),
        /// as linhas de cada série, e a legenda (se habilitada).
        /// </summary>
        /// <param name="canvas">Superfície de desenho provida pelo <see cref="GraphicsView"/>.</param>
        /// <param name="dirtyRect">Retângulo total disponível para renderização.</param>
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.SaveState();
            canvas.Antialias = true;
            canvas.FillColor = Colors.Transparent;
            canvas.FillRectangle(dirtyRect);

            // Nenhum dado -> mensagem vazia amigável
            if (Series == null || Series.Count == 0 || (Series.Count == 1 && (Series[0] == null || Series[0].Count == 0)))
            {
                DrawEmpty(canvas, dirtyRect, "Clique em Simular para ver resultados");
                canvas.RestoreState();
                return;
            }

            // ÁREA DE PLOTAGEM (reserva margens para eixos e rótulos)
            var plotRect = new RectF(
                dirtyRect.Left + LeftMargin,
                dirtyRect.Top + TopMargin,
                Math.Max(20, dirtyRect.Width - (LeftMargin + RightMargin)),
                Math.Max(20, dirtyRect.Height - (TopMargin + BottomMargin))
            );

            // Limites das séries (X = comprimento máximo; Y = min/max com padding)
            int maxLen = Series.Where(s => s != null).DefaultIfEmpty(Array.Empty<double>().ToList()).Max(s => s.Count);
            double minY = Series.Where(s => s != null).SelectMany(s => s).DefaultIfEmpty(0).Min();
            double maxY = Series.Where(s => s != null).SelectMany(s => s).DefaultIfEmpty(1).Max();
            if (Math.Abs(maxY - minY) < 1e-9) { maxY = minY + 1.0; } // evita divisão por zero
            var pad = (maxY - minY) * 0.06;
            minY -= pad; maxY += pad;

            // Eixos + grid + rótulos de ticks
            DrawAxesAndGrid(canvas, plotRect, maxLen, minY, maxY);

            // Linhas das séries
            EnsureColors(Series.Count);
            canvas.StrokeSize = StrokeSize;
            canvas.StrokeLineCap = LineCap.Round;

            float[]? pattern = LineStyle switch
            {
                LineStyleOption.Dashed => new float[] { 8, 5 },
                LineStyleOption.Dotted => new float[] { 2, 4 },
                _ => null
            };

            for (int i = 0; i < Series.Count; i++)
            {
                var s = Series[i];
                if (s == null || s.Count == 0) continue;

                canvas.StrokeColor = SeriesColors[i % SeriesColors.Count];
                canvas.StrokeDashPattern = pattern;

                var path = new PathF();
                for (int x = 0; x < s.Count; x++)
                {
                    float px = XToPx(x, maxLen, plotRect);
                    float py = YToPx(s[x], minY, maxY, plotRect);
                    if (x == 0) path.MoveTo(px, py);
                    else path.LineTo(px, py);
                }
                canvas.DrawPath(path);
            }

            if (ShowLegend)
                DrawLegend(canvas, plotRect);

            canvas.RestoreState();
        }

        /// <summary>
        /// Desenha eixos X/Y, ticks, labels e (se habilitado) linhas de grade discretas.
        /// </summary>
        /// <param name="canvas">Superfície de desenho.</param>
        /// <param name="r">Retângulo interno de plotagem.</param>
        /// <param name="maxLen">Número máximo de pontos entre todas as séries (escala X).</param>
        /// <param name="minY">Menor valor Y observado (após padding).</param>
        /// <param name="maxY">Maior valor Y observado (após padding).</param>
        private void DrawAxesAndGrid(ICanvas canvas, RectF r, int maxLen, double minY, double maxY)
        {
            canvas.StrokeSize = 1;
            canvas.StrokeDashPattern = null;
            canvas.StrokeColor = Colors.Grey;

            // Eixos
            canvas.DrawLine(r.Left, r.Bottom, r.Right, r.Bottom); // X
            canvas.DrawLine(r.Left, r.Top, r.Left, r.Bottom);     // Y

            int xTicks = 5;
            int yTicks = 5;
            var labelPaint = Colors.Grey;

            // Ticks/labels do eixo Y + grid horizontal
            for (int i = 0; i <= yTicks; i++)
            {
                float y = r.Bottom - (i / (float)yTicks) * r.Height;
                double val = minY + (i / (double)yTicks) * (maxY - minY);

                if (ShowGrid && i is > 0 and < 5)
                {
                    canvas.StrokeColor = Colors.LightGray.WithAlpha(0.35f);
                    canvas.StrokeDashPattern = new float[] { 3, 3 };
                    canvas.DrawLine(r.Left, y, r.Right, y);
                    canvas.StrokeDashPattern = null;
                    canvas.StrokeColor = Colors.Grey;
                }

                canvas.DrawLine(r.Left - 4, y, r.Left, y); // tick
                string txt = UseCurrencyFormat ? val.ToString("C2") : val.ToString("N2");
                canvas.FontSize = 11;
                canvas.FontColor = labelPaint;
                canvas.DrawString(txt, r.Left - 6, y, HorizontalAlignment.Right);
            }

            // Ticks/labels do eixo X + grid vertical
            for (int i = 0; i <= xTicks; i++)
            {
                float x = r.Left + (i / (float)xTicks) * r.Width;
                int day = maxLen <= 1 ? 0 : (int)Math.Round(i / (double)xTicks * (maxLen - 1));

                if (ShowGrid && i is > 0 and < 5)
                {
                    canvas.StrokeColor = Colors.LightGray.WithAlpha(0.35f);
                    canvas.StrokeDashPattern = new float[] { 3, 3 };
                    canvas.DrawLine(x, r.Top, x, r.Bottom);
                    canvas.StrokeDashPattern = null;
                    canvas.StrokeColor = Colors.Grey;
                }

                canvas.DrawLine(x, r.Bottom, x, r.Bottom + 4); // tick
                canvas.FontSize = 11;
                canvas.FontColor = labelPaint;
                canvas.DrawString($"D{day}", x, r.Bottom + 6, HorizontalAlignment.Center);
            }
        }

        /// <summary>
        /// Desenha a legenda para as séries, utilizando suas respectivas cores,
        /// distribuída em colunas no canto superior direito da área de plotagem.
        /// </summary>
        /// <param name="canvas">Superfície de desenho.</param>
        /// <param name="r">Retângulo de plotagem.</param>
        private void DrawLegend(ICanvas canvas, RectF r)
        {
            float itemW = 60;
            float itemH = 16;
            float pad = 6;

            int cols = Math.Max(1, (int)(r.Width / (itemW + pad)));
            float x0 = r.Right - Math.Min(Series.Count, cols) * (itemW + pad) - 10;
            float y0 = r.Top + 6;

            for (int i = 0; i < Series.Count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float x = x0 + col * (itemW + pad);
                float y = y0 + row * (itemH + pad);

                // linha colorida
                canvas.StrokeColor = SeriesColors[i % SeriesColors.Count];
                canvas.StrokeSize = 3;
                canvas.StrokeDashPattern = null;
                canvas.DrawLine(x, y + itemH / 2, x + 24, y + itemH / 2);

                // texto
                canvas.FontSize = 11;
                canvas.FontColor = Colors.Grey;
                canvas.DrawString($"Sim {i + 1}", x + 30, y + 1, HorizontalAlignment.Left);
            }
        }

        /// <summary>
        /// Desenha uma mensagem amigável no centro quando não há dados para exibir.
        /// </summary>
        /// <param name="canvas">Superfície de desenho.</param>
        /// <param name="r">Retângulo total disponível.</param>
        /// <param name="message">Texto a exibir para o usuário.</param>
        private static void DrawEmpty(ICanvas canvas, RectF r, string message)
        {
            canvas.FontColor = Colors.Grey;
            canvas.FontSize = 14;
            canvas.DrawString(message, r.Center.X, r.Center.Y, HorizontalAlignment.Center);
        }

        /// <summary>
        /// Converte um índice X (passo/“dia”) em coordenada horizontal no retângulo de plotagem.
        /// </summary>
        /// <param name="x">Índice do ponto (0..N-1).</param>
        /// <param name="maxLen">Comprimento máximo da série (N).</param>
        /// <param name="r">Retângulo de plotagem.</param>
        /// <returns>Posição X em pixels dentro de <paramref name="r"/>.</returns>
        private static float XToPx(int x, int maxLen, RectF r)
        {
            if (maxLen <= 1) return r.Left;
            return r.Left + (x / (float)(maxLen - 1)) * r.Width;
        }

        /// <summary>
        /// Converte um valor Y de dados em coordenada vertical no retângulo de plotagem.
        /// </summary>
        /// <param name="y">Valor numérico (p.ex., preço).</param>
        /// <param name="minY">Limite inferior de Y (considerando padding).</param>
        /// <param name="maxY">Limite superior de Y (considerando padding).</param>
        /// <param name="r">Retângulo de plotagem.</param>
        /// <returns>Posição Y em pixels dentro de <paramref name="r"/> (eixo invertido).</returns>
        private static float YToPx(double y, double minY, double maxY, RectF r)
        {
            double t = (y - minY) / (maxY - minY);
            return r.Bottom - (float)t * r.Height;
        }

        /// <summary>
        /// Garante que <see cref="SeriesColors"/> possui ao menos <paramref name="count"/> cores,
        /// preenchendo a partir de uma paleta padrão caso necessário.
        /// </summary>
        /// <param name="count">Quantidade mínima de cores necessárias.</param>
        private void EnsureColors(int count)
        {
            if (SeriesColors != null && SeriesColors.Count >= count) return;

            var palette = DefaultPalette();
            var list = new List<Color>(count);
            for (int i = 0; i < count; i++)
                list.Add(palette[i % palette.Count]);

            SeriesColors = list;
        }

        /// <summary>
        /// Paleta de fallback vibrante, usada quando nenhuma paleta externa é fornecida
        /// ou quando o número de séries excede as cores disponíveis.
        /// </summary>
        private static List<Color> DefaultPalette() => new()
        {
            Colors.DodgerBlue, Colors.OrangeRed, Colors.MediumSeaGreen,
            Colors.MediumOrchid, Colors.Goldenrod, Colors.CadetBlue,
            Colors.Tomato, Colors.Teal, Colors.CornflowerBlue, Colors.IndianRed
        };
    }
}