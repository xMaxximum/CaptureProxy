using CaptureProxy;

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
                }));
            };
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
