using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Windows.Forms;

namespace McpBizagi.BizagiAdapter;

public sealed partial class NativeEngine
{
    /// <summary>Run the installed editor in an explicitly requested, independently owned desktop process.</summary>
    public void RunLiveDesktop(string sessionId, Action<LiveSession> onCreated, Action<string> progress)
    {
        if (System.Reflection.Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyProductAttribute>()?.Product != "McpBizagi.LiveHost")
            throw new InvalidOperationException("The native desktop requires the dedicated live-host executable.");
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            throw new InvalidOperationException("The native desktop must run on its STA message-loop thread.");
        Initialize(progress);
        IsolateLiveInstanceDirectory();
        Type("NHunspell.dll", "NHunspell.Hunspell").GetProperty("NativeDllPath")!.SetValue(null, installation);
        if (SetDllDirectory(installation) == 0)
            throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        object settings = New(Type("CefSharp.WinForms.dll", "CefSharp.WinForms.CefSettings"));
        Set(settings, "BrowserSubprocessPath", Path.Combine(installation, "CefSharp.BrowserSubprocess.exe"));
        Set(settings, "ResourcesDirPath", installation);
        Set(settings, "LocalesDirPath", Path.Combine(installation, "locales"));
        Set(settings, "RootCachePath", Path.Combine(workRoot, "live-cache"));
        Set(settings, "LogFile", Path.Combine(workRoot, "live-cef.log"));
        // The real editor hosts both windowed and windowless Chromium controls.
        Set(settings, "WindowlessRenderingEnabled", true);
        Type("CefSharp.dll", "CefSharp.CefSharpSettings").GetProperty("WcfEnabled")!.SetValue(null, true);
        var cef = Type("CefSharp.Core.dll", "CefSharp.Cef");
        var initialize = cef.GetMethods().Single(m => m.Name == "Initialize" && m.GetParameters().Length == 2);
        if (!(bool)initialize.Invoke(null, new[] { settings, true })!)
            throw new InvalidOperationException("Native desktop Chromium initialization failed.");
        object application = Call(injector!, "Resolve", Type("BizagiModeler.exe", "BizagiProcessModeler.ProcessModelerApplication"))!;
        try
        {
            // Preserve the installed application/authentication/licensing lifecycle.
            Call(application, "Initialize");
            using var form = (Form)New(Type("Bizagi.ProcessModeler.UI.dll", "Bizagi.ProcessModeler.UI.FrmModeler"));
            form.ShowInTaskbar = true;
            using var session = new LiveSession(this, form, sessionId);
            onCreated(session);
            progress("native_live_message_loop_starting");
            Application.Run(form);
            progress("native_live_message_loop_closed");
        }
        finally { Call(application, "Dispose"); }
    }

    private void IsolateLiveInstanceDirectory()
    {
        // Native TEMP is insufficient: its named memory mapping has a fixed global
        // name. Replace only the instance-directory storage before resolving services.
        object container = injector!.GetType().GetField("_injector", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(injector)!;
        object options = Get(container, "Options");
        bool previous = (bool)Get(options, "AllowOverridingRegistrations");
        try
        {
            Set(options, "AllowOverridingRegistrations", true);
            var contract = Type("Bizagi.ProcessModeler.BusinessLogic.dll", "Bizagi.ProcessModeler.BusinessLogic.ModelerApplication.IMemoryMappedFileManager");
            Call(injector!, "RegisterInstance", contract, new LiveInstanceDirectory(contract, workRoot).GetTransparentProxy());
        }
        finally { Set(options, "AllowOverridingRegistrations", previous); }
    }

    /// <summary>Fixed native storage contract; no model access, reflection endpoint, or shared operator map.</summary>
    private sealed class LiveInstanceDirectory : RealProxy
    {
        private readonly Type contract;
        private readonly string path;
        private bool initialized;
        internal LiveInstanceDirectory(Type contract, string root) : base(contract)
        { this.contract = contract; path = Path.Combine(root, "native-instances.json"); }

        public override IMessage Invoke(IMessage message)
        {
            var call = (IMethodCallMessage)message;
            try
            {
                object? result = null;
                switch (call.MethodName)
                {
                    // The DI container checks the proxy's declared contract type.
                    case "GetType": result = contract; break;
                    case "ToString": result = "MCP-managed native instance directory"; break;
                    case "GetHashCode": result = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this); break;
                    case "Equals": result = ReferenceEquals(GetTransparentProxy(), call.Args[0]); break;
                    case "Initialize": initialized = true; break;
                    case "ReadFile":
                        RequireInitialized(); result = File.Exists(path) ? File.ReadAllText(path) : ""; break;
                    case "WriteFile": RequireInitialized(); File.WriteAllText(path, (string)call.Args[0]); break;
                    case "DisposeMemory": initialized = false; break;
                    case "DeleteTemporalMemoryFile": initialized = false; File.Delete(path); break;
                    default: throw new NotSupportedException("Unrecognized native instance-directory contract.");
                }
                return new ReturnMessage(result, null, 0, call.LogicalCallContext, call);
            }
            catch (Exception error) { return new ReturnMessage(error, call); }
        }
        private void RequireInitialized()
        { if (!initialized) throw new InvalidOperationException("Native instance directory is not initialized."); }
    }
}
