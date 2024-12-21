using CaptureProxy.HttpIO;
using CaptureProxy.MyEventArgs;
using CaptureProxy.Tunnels;
using System;
using System.Net;
using System.Net.Sockets;

namespace CaptureProxy
{
    public class Session(HttpProxy proxy, Client client) : IDisposable
    {
        private Uri? baseUri;
        private Client? remote;

        public void Dispose()
        {
            remote?.Close();
            remote?.Dispose();

            client.Close();
            client.Dispose();

            // Clean up resources
            GC.SuppressFinalize(this);
        }

        public async Task StartAsync()
        {
#if DEBUG
            proxy.Events.Log($"Session start for {client.IpPort}.");
#endif

            try
            {
                await HandleTunneling().ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            catch (Exception ex)
            {
                proxy.Events.Log("Exception: Tunnel to " + (baseUri?.Authority ?? "undefined"));
                proxy.Events.Log(ex);
            }

            proxy.Events.HandleSessionDisconnected(this, new SessionDisconnectedEventArgs(this));

#if DEBUG
            proxy.Events.Log($"Session stop for {client.IpPort}.");
#endif
        }

        private async Task<Client?> EstablishRemote(HttpRequest request, BeforeTunnelEstablishEventArgs e)
        {
            var originalRemote = $"{e.Host}:{e.Port}";

            proxy.Events.HandleBeforeTunnelEstablish(this, e);

            var updatedRemote = $"{e.Host}:{e.Port}";
            var upstreamProxy = e.UpstreamProxy != null ? ($"{e.UpstreamProxy.Host}:{e.UpstreamProxy.Port}") : null;

            if (e.Abort) return null;

            try
            {
                var remoteHost = e.UpstreamProxy?.Host ?? e.Host;
                var remotePort = e.UpstreamProxy?.Port ?? e.Port;

                var remote = new TcpClient();
                await remote.ConnectAsync(remoteHost, remotePort)
                    .WaitAsync(TimeSpan.FromSeconds(proxy.Settings.ConnectTimeout), proxy.Token)
                    .ConfigureAwait(false);

                if (remote.Connected == false)
                {
                    remote.Close();
                    remote.Dispose();
                    return null;
                }

                // Store remote client
                return new Client(proxy, remote);
            }
            catch (Exception ex)
            {
                proxy.Events.Log($"Original remote: {originalRemote}");
                proxy.Events.Log($"Updated remote: {updatedRemote}");
                proxy.Events.Log($"Upstream proxy: {upstreamProxy}");
                proxy.Events.Log(ex);
                return null;
            }
        }

        private async Task HandleTunneling()
        {
            // Handle first request
            var request = new HttpRequest(proxy);
            await request.ReadHeaderAsync(client).ConfigureAwait(false);

            // BaseURL
            baseUri = new Uri($"{request.Uri.Scheme}://{request.Uri.Authority}");

            // Khởi tạo kết nối tới địa chỉ đích
            var e = new BeforeTunnelEstablishEventArgs(baseUri.Host, baseUri.Port);

            if (proxy.Settings.UpstreamHttpProxy != null && proxy.Settings.UpstreamHttpProxy.ProxyType == ProxyType.SOCKS5)
            {
                // Read the request body
                await request.ReadBodyAsync(client).ConfigureAwait(false);

                // Create HttpClient with SOCKS5 proxy
                var handler = new HttpClientHandler()
                {
                    Proxy = new WebProxy(proxy.Settings.UpstreamHttpProxy.Host, proxy.Settings.UpstreamHttpProxy.Port),
                    UseProxy = true,
                    Credentials = new NetworkCredential(proxy.Settings.UpstreamHttpProxy.User, proxy.Settings.UpstreamHttpProxy.Pass),
                    UseDefaultCredentials = false
                };

                var httpClient = new HttpClient(handler);

                // Create HttpRequestMessage
                var httpRequestMessage = new HttpRequestMessage
                {
                    Method = new HttpMethod(request.Method.ToString()),
                    RequestUri = request.Uri,
                    Content = new StreamContent(new MemoryStream(request.Body ?? []))
                };

                foreach (var header in request.Headers.GetAll())
                {
                    httpRequestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                try
                {
                    // Send request using HttpClient
                    var responseMessage = await httpClient.SendAsync(httpRequestMessage);

                    // Process the response
                    var response = new HttpResponse(proxy)
                    {
                        StatusCode = responseMessage.StatusCode
                    };

                    foreach (var header in responseMessage.Headers)
                    {
                        response.Headers.Add(header.Key, string.Join(", ", header.Value));
                    }

                    foreach (var header in responseMessage.Content.Headers)
                    {
                        response.Headers.Add(header.Key, string.Join(", ", header.Value));
                    }

                    var responseBody = await responseMessage.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    response.SetBody(responseBody);

                    await response.WriteHeaderAsync(client).ConfigureAwait(false);
                    await response.WriteBodyAsync(client).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    proxy.Events.Log("HttpRequestException: " + ex.Message);
                    await Helper.SendBadGatewayResponse(proxy, client).ConfigureAwait(false);
                }
                catch (TaskCanceledException ex)
                {
                    proxy.Events.Log("TaskCanceledException: " + ex.Message);
                    await Helper.SendBadGatewayResponse(proxy, client).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    proxy.Events.Log("Exception: " + ex.Message);
                    await Helper.SendBadGatewayResponse(proxy, client).ConfigureAwait(false);
                }

                return;
            }

            // need to implement SOCKS shit before this, because here the CONNECT is handled

            remote = await EstablishRemote(request, e).ConfigureAwait(false);

            // return error if connection to destination can't be established
            if (remote == null)
            {
                await Helper.SendBadGatewayResponse(proxy, client).ConfigureAwait(false);
                return;
            }

            // Start tunnel
            var config = new TunnelConfiguration
            {
                Proxy = proxy,
                BaseUri = baseUri,
                Client = client,
                Remote = remote,
                e = e,
                InitRequest = request,
            };

            if (e.PacketCapture || request.Method != HttpMethod.Connect)
            {
                await new DecryptedTunnel(config).StartAsync().ConfigureAwait(false);
            }
            else
            {
                await new BufferTunnel(config).StartAsync().ConfigureAwait(false);
            }
        }
    }
}