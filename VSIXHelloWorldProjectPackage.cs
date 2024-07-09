using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.OLE.Interop;

namespace VSIXHelloWorldProject
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(VSIXHelloWorldProjectPackage.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPage(typeof(OptionPageGrid),
    "UTGenSettings", "UTGenSettings Page", 0, 0, true)]
    public sealed class VSIXHelloWorldProjectPackage : AsyncPackage
    {
        /// <summary>
        /// VSIXHelloWorldProjectPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "ad2269b0-f318-4902-80ac-908f70a0a50c";

        public OptionPageGrid page
        {
            get
            {
                return (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
            }
        }

        public string ApiEndpoint
        {
            get
            {
                return page.ApiEndpoint;
            }
        }

        public string ApiKey
        {
            get
            {
                return page.ApiKey;
            }
        }

        public string DeploymentName
        {
            get
            {
                return page.DeploymentName;
            }
        }

        public DTE2 dte;
        public DebuggerEvents debuggerEvents;
        public bool InRecordMode = false;


        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param Name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param Name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // dte = this.GetService<DTE, DTE2>() as DTE2;
            dte = await GetServiceAsync(typeof(DTE)) as DTE2;
            IVsDebugger vsDebugger = await GetServiceAsync(typeof(SVsShellDebugger)) as IVsDebugger;
            // DTE2 dte = package.GetService(typeof(DTE)) as DTE2;
            if (dte == null)
            {
                throw new InvalidOperationException("Could not get the DTE service.");
            }
            // Subscribe to debugger events  
            debuggerEvents = dte.Events.DebuggerEvents;
            // debuggerEvents.OnEnterBreakMode += OnEnterBreakMode;
            await UTGenCommand.InitializeAsync(this);
            await RecordModeStartCommand.InitializeAsync(this);
        }

        #endregion

        private void OnEnterBreakMode(dbgEventReason Reason, ref dbgExecutionAction ExecutionAction)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!InRecordMode)
            {
                return;
            }

            // This is your callback method that will be invoked when the debugger enters break mode  
            // Implement your logic here  
            // dte.Debugger.BreakpointLastHit.Enabled
            // dte.Debugger.CurrentStackFrame
            if (dte.Debugger.BreakpointLastHit.Enabled)
            {
                // IVsDebugger vsDebugger = (IVsDebugger)this.GetService(typeof(IVsDebugger));
                EnvDTE100.Debugger5 debugger = (EnvDTE100.Debugger5)dte.Debugger;

                // dte.ExecuteCommand("View.ImmediateWindow");
                dte.ExecuteCommand("Debug.Immediate");
                // dte.ExecuteCommand("Edit.Copy", "n1 = 100;");
                //dte.ExecuteCommand("Edit.Paste", "n1 = 100;");
                //EnvDTE.Window immediateWindow = dte.Windows.Item(EnvDTE.Constants.vsext_wk_ImmedWindow);
                //immediateWindow.Activate();

                // dte.Debugger.ExecuteStatement("n1 = 100;");
                debugger.Go(true);
                debugger.ExecuteStatement("n1 = 100;");
                // dte.ExecuteCommand("调试.即时", "n1 = 100;");
                // dte.ExecuteCommand("ImmediateWindow", "var assembly = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyName(new System.Reflection.AssemblyName(\"Newtonsoft.Json\"));");
                // dte.Debugger.ExecuteStatement("(new Func<string>(() => { var assembly = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(\"D:\\\\Projects\\\\.NetCore\\\\CSharpTest\\\\bin\\\\Debug\\\\net8.0\\\\Newtonsoft.Json.dll\");var type = assembly.GetType(\"Newtonsoft.Json.JsonConvert\");var methodInfo = type.GetMethod(\"SerializeObject\", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);var result = methodInfo.Invoke(null, new object[] { this }); return result.ToString(); }))()");
                // Task task = Task.Run( () => dte.Debugger.ExecuteStatement("this"));
                // task.Wait();


                EnvDTE.StackFrame sf = debugger.CurrentStackFrame;
                List<string> parameterNames = new List<string>();
                List<string> results = new List<string>();

                foreach (Expression exp in sf.Locals)
                {
                    parameterNames.Add(exp.Name);
                }
                foreach (string name in parameterNames)
                {
                    /*
                    if (Name=="this")
                    {
                        continue;
                    }
                    */
                    // Newtonsoft.Json.JsonConvert.SerializeObject(Name);
                    // var x = debugger.GetExpression($"Newtonsoft.Json.JsonConvert.SerializeObject({Name})");
                    var x = debugger.GetExpression(name);
                    Console.WriteLine($"x.Value: {x.Value}");
                    results.Add(x.Value);
                }
                Console.WriteLine($"results.Count: {results.Count}");
            }
        }

        public IVsTextView GetCurrentTextView()
        {
            var textManager = (IVsTextManager)ServiceProvider.GlobalProvider.GetService(typeof(SVsTextManager));
            if (textManager == null)
            {
                throw new InvalidOperationException("Could not get IVsTextManager service.");
            }

            IVsTextView activeView = null;
            textManager.GetActiveView(1, null, out activeView);
            return activeView;
        }
    }
}