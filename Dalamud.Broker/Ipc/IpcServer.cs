using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Grpc.Core;
using GrpcDotNetNamedPipes;
using Serilog;

namespace Dalamud.Broker.Ipc;

internal sealed class IpcServer : IDisposable
{
    public string Path { get; }

    private readonly NamedPipeServer mServer;

    public ServiceBinderBase ServiceBinder => this.mServer.ServiceBinder;

    public IpcServer(string path, IdentityReference containerId)
    {
        var currentUser = new NTAccount(Environment.UserName);

        // Create a security descriptor for the pipe.
        // - Host should have full control over the pipe.
        // - Client (sandboxed process) should have read/write(modulo WriteDacl) access to it. 
        var pipeSecurity = new PipeSecurity();
        pipeSecurity.AddAccessRule(new PipeAccessRule(currentUser, PipeAccessRights.FullControl,
                                                      AccessControlType.Allow));
        pipeSecurity.AddAccessRule(new PipeAccessRule(containerId, PipeAccessRights.ReadWrite,
                                                      AccessControlType.Allow));

        var serverOptions = new NamedPipeServerOptions
        {
            PipeSecurity = pipeSecurity
        };

        this.Path = path;

        // Create a server.
        this.mServer = new NamedPipeServer(path, serverOptions);
        this.mServer.Error += this.OnError;
    }

    private void OnError(object? sender, NamedPipeErrorEventArgs e)
    {
        Log.Error(e.Error, "An error occurred from the ipc service");
    }

    public void Start()
    {
        this.mServer.Start();
    }

    public void Dispose()
    {
        this.mServer.Error -= this.OnError;
        this.mServer.Dispose();
    }
}
