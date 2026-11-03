using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace Arreglos
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new FormArreglos());
        }
    }

    // Panel sin parpadeo para dibujar gráficos personalizados
    class PanelBuffer : Panel
    {
        public PanelBuffer()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }

    class ResultadoBench
    {
        public string Nombre;
        public double Ms;
        public long Gc;
        public long Kb;
        public Color Color;
        public string Extra;
    }

    // Programa 1: arreglos creados y redimensionados en tiempo de ejecución
    public class FormArreglos : Form
    {
        // ---------- Paleta ----------
        static readonly Color CFondo   = ColorTranslator.FromHtml("#0F172A");
        static readonly Color CCard    = ColorTranslator.FromHtml("#1E293B");
        static readonly Color CCampo   = ColorTranslator.FromHtml("#0B1220");
        static readonly Color CBorde   = ColorTranslator.FromHtml("#334155");
        static readonly Color CTexto   = ColorTranslator.FromHtml("#E2E8F0");
        static readonly Color CMuted   = ColorTranslator.FromHtml("#94A3B8");
        static readonly Color CIndigo  = ColorTranslator.FromHtml("#6366F1");
        static readonly Color CVerde   = ColorTranslator.FromHtml("#10B981");
        static readonly Color CAmbar   = ColorTranslator.FromHtml("#F59E0B");
        static readonly Color CCian    = ColorTranslator.FromHtml("#22D3EE");
        static readonly Color CRosa    = ColorTranslator.FromHtml("#F472B6");

        // ---------- Estado ----------
        int[] arreglo;                       // la referencia vive en el stack, el arreglo en el heap
        int tamAnterior = -1;                // tamaño antes de redimensionar (-1 = recién creado)
        string idActual = "";
        readonly List<ResultadoBench> resultados = new List<ResultadoBench>();

        // ---------- Controles ----------
        NumericUpDown nudTam, nudNuevo, nudN;
        Button btnCrear, btnRedim, btnBench;
        RichTextBox txtLog;
        PanelBuffer pnlViz, pnlChart;

        Font fTitulo = new Font("Segoe UI Semibold", 11f);
        Font fNormal = new Font("Segoe UI", 9.5f);
        Font fPeque  = new Font("Segoe UI", 8.5f);
        Font fMono   = new Font("Consolas", 10f);
        Font fMonoB  = new Font("Consolas", 10f, FontStyle.Bold);

        public FormArreglos()
        {
            Text = "Programa 1 - Arreglos y redimensionamiento en tiempo de ejecución";
            Width = 1180; Height = 780;
            MinimumSize = new Size(1000, 700);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = CFondo;
            ForeColor = CTexto;
            Font = fNormal;

            // ===== Registro (centro) =====
            var wrapLog = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 0, 8, 16), BackColor = CFondo };
            txtLog = new RichTextBox
            {
                Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None,
                BackColor = CCard, ForeColor = CTexto, Font = fMono, ScrollBars = RichTextBoxScrollBars.Vertical,
                DetectUrls = false
            };
            var padLog = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), BackColor = CCard };
            padLog.Controls.Add(txtLog);
            wrapLog.Controls.Add(padLog);
            wrapLog.Controls.Add(Seccion("Registro de operaciones"));

            // ===== Gráfico de benchmark (derecha) =====
            var wrapChart = new Panel { Dock = DockStyle.Right, Width = 400, Padding = new Padding(8, 0, 16, 16), BackColor = CFondo };
            pnlChart = new PanelBuffer { Dock = DockStyle.Fill, BackColor = CCard };
            pnlChart.Paint += PintarBench;
            wrapChart.Controls.Add(pnlChart);
            wrapChart.Controls.Add(Seccion("Comparativa de rendimiento"));

            // ===== Visualizador del arreglo =====
            var wrapViz = new Panel { Dock = DockStyle.Top, Height = 190, Padding = new Padding(16, 0, 16, 12), BackColor = CFondo };
            pnlViz = new PanelBuffer { Dock = DockStyle.Fill, BackColor = CCard };
            pnlViz.Paint += PintarArreglo;
            wrapViz.Controls.Add(pnlViz);
            wrapViz.Controls.Add(Seccion("Vista del arreglo en memoria"));

            // ===== Tarjetas de control =====
            var tabla = new TableLayoutPanel
            {
                Dock = DockStyle.Top, Height = 170, ColumnCount = 3, RowCount = 1,
                Padding = new Padding(10, 14, 10, 6), BackColor = CFondo
            };
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
            tabla.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            nudTam = Numerico(1, 10000000, 10, 1);
            btnCrear = Boton("Crear arreglo", CIndigo);
            btnCrear.Click += BtnCrear_Click;
            tabla.Controls.Add(Tarjeta("1 · Crear", "Tamaño inicial decidido en tiempo de ejecución", CIndigo, nudTam, btnCrear), 0, 0);

            nudNuevo = Numerico(1, 10000000, 20, 1);
            btnRedim = Boton("Redimensionar", CVerde);
            btnRedim.Click += BtnRedim_Click;
            tabla.Controls.Add(Tarjeta("2 · Redimensionar", "Array.Resize crea un arreglo nuevo y copia", CVerde, nudNuevo, btnRedim), 1, 0);

            nudN = Numerico(1000, 100000, 20000, 1000);
            btnBench = Boton("Comparar rendimiento", CAmbar);
            btnBench.ForeColor = Color.FromArgb(30, 30, 30);
            btnBench.Click += BtnBench_Click;
            tabla.Controls.Add(Tarjeta("3 · Benchmark", "Elementos a agregar uno por uno", CAmbar, nudN, btnBench), 2, 0);

            // ===== Encabezado =====
            var header = new Panel { Dock = DockStyle.Top, Height = 78 };
            header.Paint += (s, e) =>
            {
                using (var br = new LinearGradientBrush(header.ClientRectangle, CIndigo, ColorTranslator.FromHtml("#0EA5E9"), LinearGradientMode.Horizontal))
                    e.Graphics.FillRectangle(br, header.ClientRectangle);
            };
            header.Controls.Add(new Label
            {
                Text = "Arreglos en tiempo de ejecución", Font = new Font("Segoe UI Semibold", 17f),
                ForeColor = Color.White, BackColor = Color.Transparent, AutoSize = true, Location = new Point(20, 10)
            });
            header.Controls.Add(new Label
            {
                Text = "Crea, redimensiona y compara el costo de hacer crecer un arreglo",
                Font = new Font("Segoe UI", 10f), ForeColor = Color.FromArgb(225, 235, 255),
                BackColor = Color.Transparent, AutoSize = true, Location = new Point(22, 46)
            });

            // El orden importa: Fill primero, los Top al final (el último queda arriba)
            Controls.Add(wrapLog);
            Controls.Add(wrapChart);
            Controls.Add(wrapViz);
            Controls.Add(tabla);
            Controls.Add(header);

            Log("[INFO]", CCian, "Pulsa 'Crear arreglo' para comenzar.");
        }

        // ================= Helpers de UI =================

        Label Seccion(string texto)
        {
            return new Label
            {
                Text = texto.ToUpper(), Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = CMuted, BackColor = CFondo, TextAlign = ContentAlignment.MiddleLeft
            };
        }

        NumericUpDown Numerico(int min, int max, int valor, int incremento)
        {
            return new NumericUpDown
            {
                Minimum = min, Maximum = max, Value = valor, Increment = incremento, Width = 110,
                BackColor = CCampo, ForeColor = CTexto, BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11f), TextAlign = HorizontalAlignment.Right
            };
        }

        Button Boton(string texto, Color color)
        {
            var b = new Button
            {
                Text = texto, Height = 36, Width = 170, FlatStyle = FlatStyle.Flat, BackColor = color,
                ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 10f), Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(color, 0.25f);
            b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(color, 0.1f);
            return b;
        }

        Panel Tarjeta(string titulo, string descripcion, Color acento, NumericUpDown nud, Button btn)
        {
            var card = new PanelBuffer { Dock = DockStyle.Fill, Margin = new Padding(6), BackColor = CCard };
            card.Paint += (s, e) =>
            {
                using (var br = new SolidBrush(acento))
                    e.Graphics.FillRectangle(br, 0, 0, card.Width, 4);
            };
            var lt = new Label { Text = titulo, Font = fTitulo, ForeColor = CTexto, AutoSize = true, Location = new Point(16, 16), BackColor = Color.Transparent };
            var ld = new Label { Text = descripcion, Font = fPeque, ForeColor = CMuted, AutoSize = true, Location = new Point(17, 42), BackColor = Color.Transparent };
            nud.Location = new Point(16, 78);
            btn.Location = new Point(16, 116);
            card.Controls.Add(lt);
            card.Controls.Add(ld);
            card.Controls.Add(nud);
            card.Controls.Add(btn);
            return card;
        }

        static GraphicsPath Redondo(Rectangle r, int radio)
        {
            var p = new GraphicsPath();
            int d = radio * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // ================= Registro =================

        void Log(string etiqueta, Color color, string mensaje)
        {
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.SelectionLength = 0;
            txtLog.SelectionFont = fMonoB;
            txtLog.SelectionColor = color;
            txtLog.AppendText(etiqueta + " ");
            txtLog.SelectionFont = fMono;
            txtLog.SelectionColor = CTexto;
            txtLog.AppendText(mensaje + Environment.NewLine);
            txtLog.ScrollToCaret();
        }

        void Detalle(string mensaje)
        {
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.SelectionLength = 0;
            txtLog.SelectionFont = fMono;
            txtLog.SelectionColor = CMuted;
            txtLog.AppendText("         " + mensaje + Environment.NewLine);
            txtLog.ScrollToCaret();
        }

        void Separador()
        {
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.SelectionLength = 0;
            txtLog.SelectionColor = CBorde;
            txtLog.AppendText(new string('─', 48) + Environment.NewLine);
        }

        static string Id(object o) => "0x" + RuntimeHelpers.GetHashCode(o).ToString("X8");

        // ================= Dibujo del arreglo =================

        void PintarArreglo(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(CCard);

            var centro = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

            if (arreglo == null)
            {
                using (var br = new SolidBrush(CMuted))
                    g.DrawString("Aún no hay ningún arreglo. Crea uno para verlo aquí.", fNormal, br, pnlViz.ClientRectangle, centro);
                return;
            }

            // Encabezado
            using (var br = new SolidBrush(CTexto))
                g.DrawString("int[] arreglo   ·   Length = " + arreglo.Length + "   ·   heap: " + idActual, fTitulo, br, 16, 12);

            // Celdas
            int cw = 52, ch = 52, gap = 8, x0 = 16, y0 = 56;
            int cap = Math.Max(1, (pnlViz.Width - 2 * x0 + gap) / (cw + gap));
            int mostrar = Math.Min(arreglo.Length, cap);
            bool hayMas = arreglo.Length > mostrar;
            if (hayMas) mostrar = Math.Max(1, mostrar - 1);   // deja espacio para el indicador "+N"

            for (int i = 0; i < mostrar; i++)
            {
                var r = new Rectangle(x0 + i * (cw + gap), y0, cw, ch);
                bool nueva = tamAnterior >= 0 && i >= tamAnterior;
                Color c = nueva ? CVerde : CIndigo;

                using (var path = Redondo(r, 8))
                using (var fill = new SolidBrush(Color.FromArgb(nueva ? 45 : 60, c)))
                using (var pen = new Pen(c, 2f))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(pen, path);
                }
                using (var br = new SolidBrush(CTexto))
                    g.DrawString(arreglo[i].ToString(), fMonoB, br, r, centro);
                using (var br = new SolidBrush(CMuted))
                    g.DrawString(i.ToString(), fPeque, br, new Rectangle(r.X, r.Bottom + 2, r.Width, 16), centro);
            }

            if (hayMas)
            {
                var r = new Rectangle(x0 + mostrar * (cw + gap), y0, cw, ch);
                using (var br = new SolidBrush(CMuted))
                    g.DrawString("+" + (arreglo.Length - mostrar), fMonoB, br, r, centro);
            }

            // Leyenda
            int ly = pnlViz.Height - 26;
            DibujarLeyenda(g, 16, ly, CIndigo, "valor conservado / original");
            if (tamAnterior >= 0)
                DibujarLeyenda(g, 236, ly, CVerde, "espacio nuevo (inicia en 0)");
        }

        void DibujarLeyenda(Graphics g, int x, int y, Color c, string texto)
        {
            using (var br = new SolidBrush(c))
                g.FillEllipse(br, x, y + 3, 10, 10);
            using (var br = new SolidBrush(CMuted))
                g.DrawString(texto, fPeque, br, x + 16, y);
        }

        // ================= Dibujo del benchmark =================

        void PintarBench(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(CCard);

            if (resultados.Count == 0)
            {
                var centro = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                using (var br = new SolidBrush(CMuted))
                    g.DrawString("Ejecuta el benchmark para ver\nla comparativa de tiempos.", fNormal, br, pnlChart.ClientRectangle, centro);
                return;
            }

            double max = 0.001;
            foreach (var r in resultados) if (r.Ms > max) max = r.Ms;

            int w = pnlChart.Width;
            int y = 18;
            var derecha = new StringFormat { Alignment = StringAlignment.Far };

            foreach (var r in resultados)
            {
                using (var br = new SolidBrush(CTexto))
                    g.DrawString(r.Nombre, fTitulo, br, 16, y);
                using (var br = new SolidBrush(r.Color))
                    g.DrawString(r.Ms.ToString("0.0") + " ms", fMonoB, br, new RectangleF(0, y + 1, w - 16, 22), derecha);

                var pista = new Rectangle(16, y + 30, w - 32, 14);
                using (var path = Redondo(pista, 7))
                using (var br = new SolidBrush(CCampo))
                    g.FillPath(br, path);

                int ancho = Math.Max(14, (int)(pista.Width * (r.Ms / max)));
                var barra = new Rectangle(pista.X, pista.Y, Math.Min(ancho, pista.Width), pista.Height);
                using (var path = Redondo(barra, 7))
                using (var br = new SolidBrush(r.Color))
                    g.FillPath(br, path);

                string info = "GC gen0: " + r.Gc + "   ·   memoria: " + r.Kb + " KB" + (r.Extra != "" ? "   ·   " + r.Extra : "");
                using (var br = new SolidBrush(CMuted))
                    g.DrawString(info, fPeque, br, 16, y + 50);

                y += 84;
            }
        }

        // ================= Eventos =================

        void BtnCrear_Click(object sender, EventArgs e)
        {
            int n = (int)nudTam.Value;
            arreglo = new int[n];                       // tamaño decidido en tiempo de ejecución
            for (int i = 0; i < n; i++) arreglo[i] = i + 1;
            tamAnterior = -1;
            idActual = Id(arreglo);

            Log("[CREAR]", CIndigo, "Arreglo de " + n + " elementos. Identidad en heap: " + idActual);
            Detalle("Contenido: " + Resumen(arreglo));
            pnlViz.Invalidate();
        }

        void BtnRedim_Click(object sender, EventArgs e)
        {
            if (arreglo == null) { Log("[AVISO]", CAmbar, "Primero crea un arreglo."); return; }

            int nuevo = (int)nudNuevo.Value;
            string antes = Id(arreglo);
            int tamAntes = arreglo.Length;

            Array.Resize(ref arreglo, nuevo);           // crea un arreglo nuevo y copia los datos
            tamAnterior = tamAntes;
            idActual = Id(arreglo);

            Log("[REDIM]", CVerde, tamAntes + " -> " + nuevo + " elementos");
            Detalle("Antes: " + antes + "   Después: " + idActual + "  (cambió de objeto: " + (antes != idActual) + ")");
            Detalle("Contenido: " + Resumen(arreglo));
            pnlViz.Invalidate();
        }

        static string Resumen(int[] a)
        {
            int mostrar = Math.Min(a.Length, 15);
            var partes = new string[mostrar];
            for (int i = 0; i < mostrar; i++) partes[i] = a[i].ToString();
            return "[" + string.Join(", ", partes) + (a.Length > mostrar ? ", ..." : "") + "]";
        }

        void BtnBench_Click(object sender, EventArgs e)
        {
            int n = (int)nudN.Value;
            btnBench.Enabled = false;
            btnBench.Text = "Midiendo...";
            UseWaitCursor = true;
            Application.DoEvents();

            resultados.Clear();
            Separador();
            Log("[BENCH]", CAmbar, "Agregar " + n + " elementos uno por uno");

            // 1) Crecer de 1 en 1 con Array.Resize
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            long mem0 = GC.GetTotalMemory(true);
            int gen0a = GC.CollectionCount(0);
            var sw = Stopwatch.StartNew();
            int[] a = new int[0];
            for (int i = 0; i < n; i++) { Array.Resize(ref a, a.Length + 1); a[i] = i; }
            sw.Stop();
            Registrar("Array.Resize de 1 en 1", sw, gen0a, mem0, CRosa, "");

            // 2) Crecer duplicando capacidad
            GC.Collect();
            mem0 = GC.GetTotalMemory(true);
            gen0a = GC.CollectionCount(0);
            sw.Restart();
            int[] b = new int[4]; int count = 0;
            for (int i = 0; i < n; i++)
            {
                if (count == b.Length) Array.Resize(ref b, b.Length * 2);
                b[count++] = i;
            }
            sw.Stop();
            Registrar("Duplicando capacidad", sw, gen0a, mem0, CCian, "capacidad " + b.Length);

            // 3) List<int> (hace lo mismo internamente)
            GC.Collect();
            mem0 = GC.GetTotalMemory(true);
            gen0a = GC.CollectionCount(0);
            sw.Restart();
            var lista = new List<int>();
            for (int i = 0; i < n; i++) lista.Add(i);
            sw.Stop();
            Registrar("List<int>", sw, gen0a, mem0, CIndigo, "capacidad " + lista.Capacity);

            // 4) Arreglo de tamaño fijo conocido de antemano
            GC.Collect();
            mem0 = GC.GetTotalMemory(true);
            gen0a = GC.CollectionCount(0);
            sw.Restart();
            int[] c = new int[n];
            for (int i = 0; i < n; i++) c[i] = i;
            sw.Stop();
            Registrar("Arreglo de tamaño fijo", sw, gen0a, mem0, CVerde, "");

            GC.KeepAlive(a); GC.KeepAlive(b); GC.KeepAlive(lista); GC.KeepAlive(c);

            pnlChart.Invalidate();
            btnBench.Text = "Comparar rendimiento";
            btnBench.Enabled = true;
            UseWaitCursor = false;
        }

        void Registrar(string nombre, Stopwatch sw, int gen0Inicial, long memInicial, Color color, string extra)
        {
            var r = new ResultadoBench
            {
                Nombre = nombre,
                Ms = sw.Elapsed.TotalMilliseconds,
                Gc = GC.CollectionCount(0) - gen0Inicial,
                Kb = (GC.GetTotalMemory(false) - memInicial) / 1024,
                Color = color,
                Extra = extra
            };
            resultados.Add(r);

            Log("  >", color, nombre.PadRight(24) + r.Ms.ToString("0.0").PadLeft(9) + " ms | GC gen0: " + r.Gc + " | memoria: " + r.Kb + " KB" + (extra != "" ? " (" + extra + ")" : ""));
            pnlChart.Invalidate();
            pnlChart.Update();
        }
    }
}
