using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows7.Multitouch;
using Windows7.Multitouch.WPF;
using TETCSharpClient;
using TETCSharpClient.Data;
using MessageBox = System.Windows.MessageBox;
using System.IO.Ports;
using System.IO;

using System.Threading;
using System.Windows.Interop;
using System.ComponentModel;
using System.Diagnostics;

namespace Scroll
{
    public partial class MainWindow : IGazeListener
    {
        #region Variables
        private const float DPI_DEFAULT = 96f; // Resolução DPI padrão do sistema
        private const int MAX_IMAGE_WIDTH = 1600;
        private readonly double dpiScale;
        private Matrix transfrm;
        private readonly System.Windows.Forms.Timer timer;

        private int cont = 0;
        SerialPort portaSerial = new SerialPort();
        private string comando;
        private bool CONTROLE;

        System.Windows.Media.Effects.DropShadowEffect highlighted = new System.Windows.Media.Effects.DropShadowEffect();
        System.Windows.Media.Effects.DropShadowEffect normal = new System.Windows.Media.Effects.DropShadowEffect();

        #endregion

        #region Enums

        public enum DeviceCap
        {
            LOGPIXELSX = 88,
            LOGPIXELSY = 90
        }

        #endregion

        #region Constructor

        public MainWindow()
        {
            OpenSerialPort();

            InitializeComponent();
            var connectedOk = true;

            this.ContentRendered += (sender, args) => InitClient();

            //if (!GazeManager.Instance.IsActivated)
            //{
            //    Dispatcher.BeginInvoke(new Action(() => MessageBox.Show("EyeTribe Server has not been started")));
            //    connectedOk = false;
            //}
            if (!GazeManager.Instance.IsCalibrated)
            {
                //Dispatcher.BeginInvoke(new Action(() => MessageBox.Show("User is not calibrated")));
            }
            if (!connectedOk)
            {
                Close();
                return;
            }

            // Registrar evento para teclas
            KeyDown += WindowKeyDown;

            // Calcular escala em DPI
            dpiScale = CalcDpiScale();

            timer = new System.Windows.Forms.Timer { Interval = 1000 };
            timer.Tick += timer_Tick;

            //System.Windows.Media.Effects.DropShadowEffect highlighted = new System.Windows.Media.Effects.DropShadowEffect();
            highlighted.Opacity = 1;
            highlighted.ShadowDepth = 0;
            highlighted.BlurRadius = 125;
            highlighted.Color = Colors.White;

            //System.Windows.Media.Effects.DropShadowEffect normal = new System.Windows.Media.Effects.DropShadowEffect();
            normal.Opacity = 1;
            normal.ShadowDepth = 0;
            normal.BlurRadius = 0;
            normal.Color = Colors.White;
        }

        private void InitClient()
        {
            // Ativar/conectar cliente
            GazeManager.Instance.Activate(GazeManager.ApiVersion.VERSION_1_0, GazeManager.ClientMode.Push);
            GazeManager.Instance.AddGazeListener(this);
            // Verifica se o servidor está executando
            if (!IsServerProcessRunning())
                StartServerProcess();
        }

        #endregion

        #region Public methods
        public void OnGazeUpdate(GazeData gazeData)
        {
            var x = (int)Math.Round(gazeData.SmoothedCoordinates.X, 0);
            var y = (int)Math.Round(gazeData.SmoothedCoordinates.Y, 0);
            if (x == 0 & y == 0) return;
            // Invocar thread para atualizar interface
            Dispatcher.BeginInvoke(new Action(() => UpdateUI(x, y)));
        }

        public void OpenSerialPort()
        {
            portaSerial = new SerialPort("COM3", 9600, Parity.None, 8, StopBits.One)
            {
                Handshake = Handshake.None,
                ReadTimeout = 500,
                WriteTimeout = 500
            };

            try
            {
                // Abre uma conexão com a porta especificada
                Dispatcher.BeginInvoke(new MethodInvoker(() => portaSerial.Open()));
                MessageBox.Show("Conexão feita com sucesso.");
                // Inicia a cadeira com o comando "Parar"
                portaSerial.Write("2");

            }
            catch (System.IO.IOException e)
            {
                MessageBox.Show("O dispositivo não está na porta especificada.");
                this.Close();
            }
            return;
        }

        #endregion

        #region Private methods

        private void WindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.VolumeDown || e.Key == Key.VolumeUp || e.Key == Key.Escape)
                Close();
        }

        private void UpdateUI(int x, int y)
        {
            // Atualiza posição do ponteiro
            if (GazePointer.Visibility == Visibility.Visible)
            {
                var relativePt = new Point(x, y);
                relativePt = transfrm.Transform(relativePt);
                Canvas.SetLeft(GazePointer, relativePt.X - GazePointer.Width / 2);
                Canvas.SetTop(GazePointer, relativePt.Y - GazePointer.Height / 2);
            }
            // Verifica a área em que a direção do olhar se encontra
            CheckArea(x, y);
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            CONTROLE = false;
            cont++;

            if (cont >= 3)
            {    
                cont = 0;            
                //MessageBox.Show(comando);
                portaSerial.Write(comando); // Sem comentários.
                timer.Enabled = false;
                timer.Stop();
            }
        }
                
        
        private void CheckArea(int x, int y)
        {        
            ClampMouse(ref x, ref y);

            // Calcula a largura e altura da tela e divide o grid apropriadamente
            var h = Screen.PrimaryScreen.Bounds.Height;
            var g = Screen.PrimaryScreen.Bounds.Width;

            var InicioX = g * 0.33;
            var FimX = g * 0.66;
            var InicioY = h * 0.33;
            var FimY = h * 0.66;

            if ((x > 0) && (x < InicioX) && (y > 0) && (y < InicioY))
            {
                //status = "1";
                Dispatcher.BeginInvoke(new Action(() => ArrowUp.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => ArrowLeft.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => SquareStop.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => ArrowRight.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => ArrowDown.Effect = normal));
                timer.Start();
              
            }

            else if ((x > InicioX) && (x < FimX) && (y > 0) && (y < InicioY) && CONTROLE == false)
            {
                CONTROLE = true;
                comando = "1";
                //status = "frente";
                //portaSerial.Write(comando);
                Dispatcher.BeginInvoke(new Action(() => ArrowUp.Effect = highlighted));
                Dispatcher.BeginInvoke(new Action(() => ArrowLeft.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => SquareStop.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => ArrowRight.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => ArrowDown.Effect = normal));
                timer.Start();

            }
            else if ((x > FimX) && (x < g ) && (y > 0) && (y < InicioY))
            {
                //status = "3";
                Dispatcher.BeginInvoke(new Action(() => ArrowUp.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => ArrowLeft.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => SquareStop.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => ArrowRight.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => ArrowDown.Effect = normal));
                timer.Start();

            }
            else if ((x > 0) && (x < InicioX) && (y > InicioY) && (y < FimY) && CONTROLE == false)
            {
                CONTROLE = true;
                comando = "4";
                //status = "esquerda";
                //portaSerial.Write(comando);
                Dispatcher.BeginInvoke(new Action(() => ArrowUp.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => ArrowLeft.Effect = highlighted));
                Dispatcher.BeginInvoke(new Action(() => SquareStop.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => ArrowRight.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => ArrowDown.Effect = normal));
                timer.Start();

            }
            else if ((x > InicioX) && (x < FimX) && (y > InicioY) && (y < FimY) && CONTROLE == false)
            {
                CONTROLE = true;
                comando = "2";
                //status = "parar";
                //portaSerial.Write(comando);
                Dispatcher.BeginInvoke(new Action(() => ArrowUp.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => ArrowLeft.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => SquareStop.Effect = highlighted));
                Dispatcher.BeginInvoke(new Action(() => ArrowRight.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => ArrowDown.Effect = normal));
                timer.Start();

            }
            else if ((x > FimX) && (x < g) && (y > InicioY) && (y < FimY) && CONTROLE == false)
            {
                CONTROLE = true;
                comando = "3";
                //status = "direita";
                //portaSerial.Write(comando);       
                Dispatcher.BeginInvoke(new Action(() => ArrowUp.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => ArrowLeft.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => SquareStop.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => ArrowRight.Effect = highlighted));
                Dispatcher.BeginInvoke(new Action(() => ArrowDown.Effect = normal));
                timer.Start();

            }
            else if ((x > 0) && (x < InicioX) && (y > FimY) && (y < h))
            {
                //status = "7";
                Dispatcher.BeginInvoke(new Action(() => ArrowUp.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => ArrowLeft.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => SquareStop.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => ArrowRight.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => ArrowDown.Effect = normal));
                timer.Start();

            }
            else if ((x > InicioX) && (x < FimX) && (y > FimY) && (y < h) && CONTROLE == false)
            {
                CONTROLE = true;
                comando = "5";
                //status = "trás";
                //portaSerial.Write(comando);
                Dispatcher.BeginInvoke(new Action(() => ArrowUp.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => ArrowLeft.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => SquareStop.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => ArrowRight.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => ArrowDown.Effect = highlighted));
                timer.Start();

            }
            else if ((x > FimX) && (x < g) && (y > FimY) && (y < h))
            {
                //status = "9";
                Dispatcher.BeginInvoke(new Action(() => ArrowUp.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => ArrowLeft.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => SquareStop.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => ArrowRight.Effect = normal));
                Dispatcher.BeginInvoke(new Action(() => ArrowDown.Effect = normal));
                timer.Start();
            }
        }

        
        private static void ClampMouse(ref int x, ref int y)
        {
            var w = Screen.PrimaryScreen.Bounds.Width;
            var h = Screen.PrimaryScreen.Bounds.Height;

            if (x >= w)
                x = w;
            else if (x <= 0)
                x = 0;

            if (y >= h)
                y = h;
            else if (y <= 0)
                y = 0;
        }

        private static void CleanUp()
        {
            GazeManager.Instance.Deactivate();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            CleanUp();
            base.OnClosing(e);
        }

        private static double CalcDpiScale()
        {
            return DPI_DEFAULT / GetSystemDpi().X;
        }

        private static bool IsServerProcessRunning()
        {
            try
            {
                foreach (Process p in Process.GetProcesses())
                {
                    if (p.ProcessName.ToLower() == "eyetribe")
                        return true;
                }
            }
            catch (Exception)
            { }

            return false;
        }

        private static void StartServerProcess()
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.WindowStyle = ProcessWindowStyle.Minimized;
            psi.FileName = GetServerExecutablePath();

            if (psi.FileName == string.Empty || File.Exists(psi.FileName) == false)
                return;

            Process processServer = new Process();
            processServer.StartInfo = psi;
            processServer.Start();

            Thread.Sleep(3000); // Espera a thread para a busca
        }

        private static string GetServerExecutablePath()
        {
            // Checar diretório padrão do servidor         
            const string x86 = "C:\\Program Files (x86)\\EyeTribe\\Server\\EyeTribe.exe";
            if (File.Exists(x86))
                return x86;

            const string x64 = "C:\\Program Files\\EyeTribe\\Server\\EyeTribe.exe";
            if (File.Exists(x64))
                return x64;

            // Se não for encontrado, deixa o usuário selecionar o arquivo
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.DefaultExt = ".exe";
            dlg.Title = "Por favor selecione o executável do servidor Eye Tribe";
            dlg.Filter = "Executable Files (*.exe)|*.exe";

            //if (dlg.ShowDialog() == true)
            //    return dlg.FileName;

            return string.Empty;
        }

        #endregion

        #region Native methods

        public static Point GetSystemDpi()
        {
            Point result = new Point();
            IntPtr hDc = GetDC(IntPtr.Zero);
            result.X = GetDeviceCaps(hDc, (int)DeviceCap.LOGPIXELSX);
            result.Y = GetDeviceCaps(hDc, (int)DeviceCap.LOGPIXELSY);
            ReleaseDC(IntPtr.Zero, hDc);
            return result;
        }

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

        #endregion
    }
}
