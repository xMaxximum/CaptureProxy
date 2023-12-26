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
            };

            CaptureProxy.Events.EstablishRemote += Events_EstablishRemote;
            CaptureProxy.Events.BeforeRequest += Events_BeforeRequest;
        }

        private void Events_EstablishRemote(object? sender, EstablishRemoteEventArgs e)
        {
            e.PacketCapture = true;
        }

        private void Events_BeforeRequest(object? sender, BeforeRequestEventArgs e)
        {
            e.Response = new HttpResponse();
            e.Response.StatusCode = HttpStatusCode.OK;
            e.Response.SetBody("This is my custom response.");
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
    }
}
