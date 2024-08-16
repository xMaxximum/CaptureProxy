using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CaptureProxy.Tunnels
{
    internal abstract class BaseTunnel
    {
        protected readonly TunnelConfiguration configuration;

        public BaseTunnel(TunnelConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public async Task StartAsync()
        {
            await Task.WhenAll([
                ClientToRemote(),
                RemoteToClient(),
            ]);
        }

        protected abstract Task RemoteToClient();

        protected abstract Task ClientToRemote();
    }
}
