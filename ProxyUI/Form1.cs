using CaptureProxy;
using CaptureProxy.MyEventArgs;
using System.Net;
using System.Text;

namespace ProxyUI
{
    public partial class Form1 : Form
    {
        HttpProxy proxy = new HttpProxy(8877);

        public Form1()
        {
            InitializeComponent();

            button1.Enabled = true;
            button2.Enabled = false;

            CaptureProxy.Events.Logger = (string message) =>
            {
                message = $"[{DateTime.Now}] {message}\r\n";
                Invoke(new Action(() =>
                {
                    richTextBox1.AppendText(message);
                    richTextBox1.ScrollToCaret();
                }));

                File.AppendAllText("logs.txt", message);
            };

            CaptureProxy.Events.BeforeTunnelConnect += Events_BeforeTunnelConnect;
            CaptureProxy.Events.BeforeRequest += Events_BeforeRequest;
            CaptureProxy.Events.BeforeResponse += Events_BeforeResponse;
        }

        private void Events_BeforeTunnelConnect(object? sender, BeforeTunnelEstablishEventArgs e)
        {
            e.PacketCapture = true;
        }

        private void Events_BeforeRequest(object? sender, BeforeRequestEventArgs e)
        {
            e.CaptureResponse = true;
        }

        private void Events_BeforeResponse(object? sender, BeforeResponseEventArgs e)
        {
            e.Response.DecodeBody();
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
                successful = CertMaker.RemoveCertByCommonName(CertMaker.CommonName);
            }

            button1.Enabled = true;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            button2_Click(null, null);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            label1.Text = "Session: " + proxy.SessionCount;
        }
    }
}
