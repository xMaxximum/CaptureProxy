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
                await ProcessConnectRequest(configuration.InitRequest).ConfigureAwait(false);
                initRequestProcessed = true;
            }

            // Start transferring
            while (true)
            {
                if (configuration.Proxy.Token.IsCancellationRequested) break;

                using var request = await ClientToRemote().ConfigureAwait(false);
                if (request == null) break;

                await RemoteToClient(request).ConfigureAwait(false);
            }
        }

        private async Task ProcessConnectRequest(HttpRequest request)
        {
            if (!configuration.e.UpstreamProxy)
            {
                await Helper.SendConnectedResponse(configuration.Proxy, configuration.Client).ConfigureAwait(false);

                configuration.Client.AuthenticateAsServer(configuration.BaseUri.Host);
                configuration.Remote.AuthenticateAsClient(configuration.BaseUri.Host);
                useSslStream = true;

                return;
            }

            if (configuration.e.ProxyUser != null && configuration.e.ProxyPass != null)
            {
                request.Headers.SetProxyAuthorization(configuration.e.ProxyUser, configuration.e.ProxyPass);
            }

            await request.WriteHeaderAsync(configuration.Remote).ConfigureAwait(false);

            var response = new HttpResponse(configuration.Proxy);
            await response.ReadHeaderAsync(configuration.Remote).ConfigureAwait(false);
            await response.ReadBodyAsync(configuration.Remote).ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                await Helper.SendBadGatewayResponse(configuration.Proxy, configuration.Client).ConfigureAwait(false);
                return;
            }

            await response.WriteHeaderAsync(configuration.Client).ConfigureAwait(false);

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
                request = new HttpRequest(configuration.Proxy);
                await request.ReadHeaderAsync(configuration.Client, configuration.BaseUri).ConfigureAwait(false);
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
                await request.WriteHeaderAsync(configuration.Remote).ConfigureAwait(false);
                await request.TransferBodyAsync(configuration.Client, configuration.Remote).ConfigureAwait(false);

                return request;
            }

            // Read body from client stream if needed
            await request.ReadBodyAsync(configuration.Client).ConfigureAwait(false);

            // Before request event
            var e = new BeforeRequestEventArgs(request);
            if (request.Method != HttpMethod.Connect)
            {
                configuration.Proxy.Events.HandleBeforeRequest(this, e);
            }

            // Write custom respose if exists
            if (e.Response != null)
            {
                await e.Response.WriteHeaderAsync(configuration.Client).ConfigureAwait(false);
                await e.Response.WriteBodyAsync(configuration.Client).ConfigureAwait(false);
                return null;
            }

            // Store original request
            var originRequest = request.Clone();

            // Update host header with request uri host
            //request.Headers.AddOrReplace("host", request.Uri.Authority);

            // Set proxy authorization if needed
            if (!useSslStream && configuration.e.UpstreamProxy && configuration.e.ProxyUser != null && configuration.e.ProxyPass != null)
            {
                request.Headers.SetProxyAuthorization(configuration.e.ProxyUser, configuration.e.ProxyPass);
            }

            // Write to remote stream
            await request.WriteHeaderAsync(configuration.Remote).ConfigureAwait(false);
            await request.WriteBodyAsync(configuration.Remote).ConfigureAwait(false);

            // Return original request
            return originRequest;
        }

        private async Task RemoteToClient(HttpRequest request)
        {
            // Read response header
            using var response = new HttpResponse(configuration.Proxy);
            await response.ReadHeaderAsync(configuration.Remote).ConfigureAwait(false);

            // Stop if upstream proxy authenticate failed
            if (!useSslStream && configuration.e.UpstreamProxy && response.StatusCode == HttpStatusCode.ProxyAuthenticationRequired)
            {
                await Helper.SendBadGatewayResponse(configuration.Proxy, configuration.Client).ConfigureAwait(false);
                return;
            }

            // Trigger before header response event
            var beforeHeaderEvent = new BeforeHeaderResponseEventArgs(request, response);
            if (configuration.e.PacketCapture)
            {
                configuration.Proxy.Events.HandleBeforeHeaderResponse(this, beforeHeaderEvent);
            }

            // If CaptureBody disabled
            if (!beforeHeaderEvent.CaptureBody)
            {
                // Write to client stream
                await response.WriteHeaderAsync(configuration.Client).ConfigureAwait(false);
                await response.TransferBodyAsync(configuration.Remote, configuration.Client).ConfigureAwait(false);
                return;
            }

            // Handle event-stream response
            if (response.EventStream)
            {
                // Write header to client stream
                await response.WriteHeaderAsync(configuration.Client).ConfigureAwait(false);

                while (true)
                {
                    if (configuration.Proxy.Token.IsCancellationRequested) break;

                    // Read body from remote stream
                    await response.ReadEventStreamBody(configuration.Remote).ConfigureAwait(false);

                    // Trigger before body response event
                    var beforeBodyEvent = new BeforeBodyResponseEventArgs(request, response);
                    configuration.Proxy.Events.HandleBeforeBodyResponse(this, beforeBodyEvent);

                    // Write body to client stream
                    await response.WriteEventStreamBody(configuration.Client).ConfigureAwait(false);
                }

                return;
            }

            // Otherwise, normal response
            {
                // Read body from remote stream
                await response.ReadBodyAsync(configuration.Remote).ConfigureAwait(false);

                // Trigger before body response event
                var beforeBodyEvent = new BeforeBodyResponseEventArgs(request, response);
                configuration.Proxy.Events.HandleBeforeBodyResponse(this, beforeBodyEvent);

                // Write body to client stream
                await response.WriteHeaderAsync(configuration.Client).ConfigureAwait(false);
                await response.WriteBodyAsync(configuration.Client).ConfigureAwait(false);
            }
        }
    }
}