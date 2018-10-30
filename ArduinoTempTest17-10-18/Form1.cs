using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management;
using System.Windows.Forms;
using System.IO.Ports;
using System.IO;
using System.Threading;

namespace ArduinoTempTest17_10_18
{
    public partial class Form1 :Form
    {
        byte retryCounter;
        bool shownBalloon = false; // Om programmet har visat notifikation eller inte
        string ArduinoTempPort;
        string buffer;

        SerialPort ar1; // Den seriella porten
        private static bool firstOpen; // Används för WriteText().

        public Form1() {
            InitializeComponent();

            ar1 = new SerialPort {
                PortName = "COM1", // Måste populeras med någonting i början, kommer att ändras senare.
                BaudRate = 9600,
                ReadTimeout = 40000 // Ha detta längre än det intervallet som det tar att hämta data, dvs att om intervallet för varje temperaturmätning så måste timeout vara över 30s.
            };
        }

        private void Form1_Load(object sender,EventArgs e) {
            this.WindowState = FormWindowState.Minimized; // Minimerar programmet när det laddas.
            notifyIcon1.Visible = true; // Visar en tray-icon.

            retry: // Label, bör ej användas 'egentligen' - men nu blev det så pga föregående projekt använde sig av label.
            ArduinoTempPort = autodetectTempArduino(); // Hämtar arduino-porten.

            if (ArduinoTempPort == null) {
                notifyIcon1.Text = "Arduino Temperature (Disconnected)";
                retryCounter++;
                if (retryCounter >= 5 && shownBalloon == false) {
                    showError();
                    shownBalloon = true;
                }
                Thread.Sleep(1000);
                goto retry; // Försöker igen efter en delay
            }
            else {
                label1.Text = ArduinoTempPort;
                string tempData;
                notifyIcon1.Text = "Arduino Temperature (Connected)";
                if (shownBalloon == true) {
                    showReconnect();
                    shownBalloon = false;
                }
                while (true) {
                    try {
                        tempData = DateTime.Now.ToString() + " : " + ar1.ReadLine();
                        WriteTxt(tempData);
                    }
                    catch {
                        goto retry; // Försöker igen om det visar sig att någonting har hänt med den seriella porten.
                    }
                }
            }

        }

        private string autodetectTempArduino() { // Gör en handskakning med en arduino
            string[] ports = SerialPort.GetPortNames(); // Hämtar alla tillgängliga seriella portar
            foreach (string port in ports) { // Sätter 'port' till ett index av 'ports', börjar med första och loopar sedan igenom alla funna seriella portar.
                try {
                    buffer = "";

                    ar1.PortName = port; 
                    ar1.Open();
                    ar1.DiscardInBuffer(); // Slänger det som väntade på att bli plockat.
                    ar1.Write("T"); // Skriver bokstaven T i ASCII.
                    Thread.Sleep(100); // Väntar 100ms. 10ms är tillräckligt för en Genuino R3, men inte en Nano med CH340 USB-To-Serial.

                    buffer += ar1.ReadExisting(); // Läser respektive COM-port och sätter data i en hållvariabel
                    if (buffer.Contains("!")) { // Om buffer inehåller en karaktär som arduinon ska svara med, returnera porten.
                        return port;
                    }
                    ar1.Close();
                }
                catch {
                    continue; // Hoppar över nuvarande loop och fortsätter på nästa index av ports.
                }
            }
            return null;
        }

        static void WriteTxt(string parsedData) // Tar någon form av data, sätter tidstämpel och lägger det i en textfil.
{
            string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + @"\Textdump\Temperature\TemperatureData.txt"; // Genererar en väg till temperatur-data.
            if (!File.Exists(path)) { // Om filen inte redan finns - fortsätt.
                FileRetry:
                try {
                    File.Create(path).Dispose(); // Skapar textdokumentet
                    //Console.WriteLine("Log file created");
                }
                catch (DirectoryNotFoundException) // Om inte ens mappen finns, skapa den.
                {
                    if (Directory.Exists(path)) {
                       // Console.WriteLine("Something is really broken");
                        return;
                    }
                    DirectoryInfo di = Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + @"\Textdump\Temperature\"); // Skapar ny mapp
                    //Console.WriteLine("Created new directory in: " + Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
                    Thread.Sleep(1000);
                    goto FileRetry; // Återgår till label: FileRetry
                }
                TextWriter tw = new StreamWriter(path); // Öppnar textwriter, skriver ut datumet och stänger sedan textwriter.
                tw.WriteLine(DateTime.Now.ToString());
                tw.Close();
            }
            else if (File.Exists(path)) { // Om loggfilen finns, lägg på den data som skickas.
                using (var tw = new StreamWriter(path,true)) { // using kommer att kasta bort resurserna(minnesplatsen) till TextWriter när detta blocket är färdigt.
                    if (firstOpen == false) {
                        firstOpen = true;
                    }
                    tw.WriteLine(parsedData); // Skriver ut det som finns i dokumentet.
                    tw.Close();
                }
            }
        }

        void showError() { // Visar error notifikation i 4s.
            notifyIcon1.BalloonTipIcon = ToolTipIcon.Error;
            notifyIcon1.BalloonTipText = "Temperature controller not connected";
            notifyIcon1.ShowBalloonTip(4000);
        }
        void showReconnect() { // Visar reconnect notifikation i 4s.
            notifyIcon1.BalloonTipText = "Connected, printing data";
            notifyIcon1.BalloonTipIcon = ToolTipIcon.Info;
            notifyIcon1.ShowBalloonTip(4000);
        }

        private void Form1_Resize(object sender,EventArgs e) { // Hanterar en resize event, dvs när fönstret minimeras. Gömmer de synliga spåren av programmet när det är minimerat.
            if (this.WindowState == FormWindowState.Minimized) {
                Hide();
            }
        }
    }
}
