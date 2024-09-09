using CaptureProxy;
using CaptureProxy.MyEventArgs;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace ProxyUI
{
    public partial class Form1 : Form
    {
        private readonly HttpProxy proxy = new HttpProxy(8877);

        public Form1()
        {
            InitializeComponent();

            button1.Enabled = true;
            button2.Enabled = false;

            proxy.Events.LogReceived += Events_LogReceived;
            proxy.Events.BeforeTunnelEstablish += Events_BeforeTunnelEstablish;
            proxy.Events.BeforeRequest += Events_BeforeRequest;
            proxy.Events.BeforeHeaderResponse += Events_BeforeHeaderResponse;
            proxy.Events.BeforeBodyResponse += Events_BeforeBodyResponse;
        }

        private void Events_LogReceived(object? sender, string message)
        {
            message = $"[{DateTime.Now}] {message}\r\n";

            try
            {
                Invoke(new Action(() =>
                {
                    richTextBox1.AppendText(message);
                    richTextBox1.ScrollToCaret();
                }));
            }
            catch { }

            //File.AppendAllText("logs.txt", message);
        }

        private void Events_BeforeTunnelEstablish(object? sender, BeforeTunnelEstablishEventArgs e)
        {
            
        }

        private void Events_BeforeRequest(object? sender, BeforeRequestEventArgs e)
        {

        }

        private void Events_BeforeHeaderResponse(object? sender, BeforeHeaderResponseEventArgs e)
        {

        }

        private void Events_BeforeBodyResponse(object? sender, BeforeBodyResponseEventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;

            if (CertMaker.InstallCert(CertMaker.CaCert) == false)
            {
                button1.Enabled = true;
                return;
            }

            proxy.Start();

            button2.Enabled = true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            button2.Enabled = false;

            proxy.Stop();

            bool successful = false;
            while (!successful)
            {
                successful = CertMaker.RemoveCertsByCommonName(CertMaker.CommonName);
            }

            button1.Enabled = true;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            button2_Click(sender, e);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            label1.Text = "Session: " + proxy.SessionCount;
        }
    }
}
