using CaptureProxy.HttpIO;
using CaptureProxy.MyEventArgs;
using System.Net;
using System.Text;

namespace CaptureProxy.Tunnels
{
    internal class DecryptedTunnel(TunnelConfiguration configuration)
    {
        private bool initRequestProcessed = false;
        private bool useSslStream = false;

        public async Task StartAsync()
        {
            if (configuration.InitRequest.Method == HttpMethod.Connect)
            {
                await ProcessConnectRequest(configuration.InitRequest);
                initRequestProcessed = true;
            }

            // Start transferring
            while (Settings.ProxyIsRunning)
            {
                //await Task.Delay(10);

                using var request = await ClientToRemote();
                if (request == null) break;

                await RemoteToClient(request);
            }
        }

        private async Task ProcessConnectRequest(HttpRequest request)
        {
            if (!configuration.e.UpstreamProxy)
            {
                await Helper.SendConnectedResponse(configuration.Client);

                configuration.Client.AuthenticateAsServer(configuration.BaseUri.Host);
                configuration.Remote.AuthenticateAsClient(configuration.BaseUri.Host);
                useSslStream = true;

                return;
            }

            if (configuration.e.ProxyUser != null && configuration.e.ProxyPass != null)
            {
                request.Headers.SetProxyAuthorization(configuration.e.ProxyUser, configuration.e.ProxyPass);
            }

            await request.WriteHeaderAsync(configuration.Remote);

            var response = new HttpResponse();
            await response.ReadHeaderAsync(configuration.Remote);
            await response.ReadBodyAsync(configuration.Remote);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                await Helper.SendBadGatewayResponse(configuration.Client);
                return;
            }

            await response.WriteHeaderAsync(configuration.Client);

            configuration.Client.AuthenticateAsServer(configuration.BaseUri.Host);
            configuration.Remote.AuthenticateAsClient(configuration.BaseUri.Host);
            useSslStream = true;

            return;
        }

        private async Task<HttpRequest?> ClientToRemote()
        {
            // Read request header
            var request = configuration.InitRequest;
            if (initRequestProcessed)
            {
                request = new HttpRequest();
                await request.ReadHeaderAsync(configuration.Client, configuration.BaseUri);
            }
            else
            {
                initRequestProcessed = true;
            }

            // If PacketCapture is disabled
            if (!configuration.e.PacketCapture)
            {
                // Set proxy authorization if needed
                if (!useSslStream && configuration.e.UpstreamProxy && configuration.e.ProxyUser != null && configuration.e.ProxyPass != null)
                {
                    request.Headers.SetProxyAuthorization(configuration.e.ProxyUser, configuration.e.ProxyPass);
                }

                // Write to remote stream
                await request.WriteHeaderAsync(configuration.Remote);
                await request.TransferBodyAsync(configuration.Client, configuration.Remote);

                return request;
            }

            // Read body from client stream if needed
            await request.ReadBodyAsync(configuration.Client);

            // Before request event
            var e = new BeforeRequestEventArgs(request);
            if (request.Method != HttpMethod.Connect)
            {
                Events.HandleBeforeRequest(this, e);
            }

            // Write custom respose if exists
            if (e.Response != null)
            {
                await e.Response.WriteHeaderAsync(configuration.Client);
                await e.Response.WriteBodyAsync(configuration.Client);
                return null;
            }

            // Store original request
            var originRequest = Helper.DeepClone(request);

            // Update host header with request uri host
            //request.Headers.AddOrReplace("host", request.Uri.Authority);

            // Set proxy authorization if needed
            if (!useSslStream && configuration.e.UpstreamProxy && configuration.e.ProxyUser != null && configuration.e.ProxyPass != null)
            {
                request.Headers.SetProxyAuthorization(configuration.e.ProxyUser, configuration.e.ProxyPass);
            }

            // Write to remote stream
            await request.WriteHeaderAsync(configuration.Remote);
            await request.WriteBodyAsync(configuration.Remote);

            // Return original request
            return originRequest;
        }

        private async Task RemoteToClient(HttpRequest request)
        {
            // Read response header
            using var response = new HttpResponse();
            await response.ReadHeaderAsync(configuration.Remote);

            // Stop if upstream proxy authenticate failed
            if (!useSslStream && configuration.e.UpstreamProxy && response.StatusCode == HttpStatusCode.ProxyAuthenticationRequired)
            {
                await Helper.SendBadGatewayResponse(configuration.Client);
                return;
            }

            // Trigger before header response event
            var beforeHeaderEvent = new BeforeHeaderResponseEventArgs(request, response);
            if (configuration.e.PacketCapture)
            {
                Events.HandleBeforeHeaderResponse(this, beforeHeaderEvent);
            }

            // If CaptureBody disabled
            if (!beforeHeaderEvent.CaptureBody)
            {
                // Write to client stream
                await response.WriteHeaderAsync(configuration.Client);
                await response.TransferBodyAsync(configuration.Remote, configuration.Client);
                return;
            }

            // Handle event-stream response
            if (response.EventStream)
            {
                // Write header to client stream
                await response.WriteHeaderAsync(configuration.Client);

                while (Settings.ProxyIsRunning)
                {
                    // Read body from remote stream
                    await response.ReadEventStreamBody(configuration.Remote);

                    // Trigger before body response event
                    var beforeBodyEvent = new BeforeBodyResponseEventArgs(request, response);
                    Events.HandleBeforeBodyResponse(this, beforeBodyEvent);

                    // Write body to client stream
                    await response.WriteEventStreamBody(configuration.Client);
                }

                return;
            }

            // Otherwise, normal response
            {
                // Read body from remote stream
                await response.ReadBodyAsync(configuration.Remote);

                // Trigger before body response event
                var beforeBodyEvent = new BeforeBodyResponseEventArgs(request, response);
                Events.HandleBeforeBodyResponse(this, beforeBodyEvent);

                // Write body to client stream
                await response.WriteHeaderAsync(configuration.Client);
                await response.WriteBodyAsync(configuration.Client);
            }
        }
    }
}