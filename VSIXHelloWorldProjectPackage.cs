using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

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

        public string ApiType
        {
            get
            {
                return page.ApiType;
            }
        }

        public string ApiEndpoint
        {
            get
            {
                return page.ApiEndpoint;
            }
        }

        public string ApiVersion
        {
            get
            {
                return page.ApiVersion;
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

        public string ModelName
        {
            get
            {
                return page.ModelName;
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
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // dte = this.GetService<DTE, DTE2>() as DTE2;
            dte = await GetServiceAsync(typeof(DTE)) as DTE2;
            // DTE2 dte = package.GetService(typeof(DTE)) as DTE2;
            if (dte == null)
            {
                throw new InvalidOperationException("Could not get the DTE service.");
            }
            // Subscribe to debugger events  
            debuggerEvents = dte.Events.DebuggerEvents;
            debuggerEvents.OnEnterBreakMode += OnEnterBreakMode;
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
                var tag = dte.Debugger.BreakpointLastHit.Tag;
                StackFrame stackFrame = dte.Debugger.CurrentStackFrame;
                foreach (Expression variable in stackFrame.Locals)
                {
                    string variableName = variable.Name;
                    string variableValue = variable.Value;
                    string variableType = variable.Type;
                    Console.WriteLine($"variableName: {variableName}\n variableValue: {variableValue}\n variableType: {variableType}\n\n\n");
                    // If you need to check if it's a parameter, you might need additional logic here  

                    // Do something with the variable information  
                }
            }
        }
    }
}
