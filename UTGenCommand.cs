using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.Settings;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Threading;

namespace VSIXHelloWorldProject
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class UTGenCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("c5ec4415-d494-46fd-b323-adca902d27e8");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly VSIXHelloWorldProjectPackage package;

        private string solutionDirectory;
        private string apiType;
        private string apiEndpoint;
        private string apiVersion;
        private string apiKey;
        private string deploymentName;
        private string modelName;
        private string copilotPlaygroundPath;
        private string outputCalcProjPath;
        private string[] funcIORecFiles;

        /// <summary>
        /// Initializes a new instance of the <see cref="UTGenCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private UTGenCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            this.package = package as VSIXHelloWorldProjectPackage ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static UTGenCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in UTGenCommand's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new UTGenCommand(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            solutionDirectory = Path.GetDirectoryName(package.dte.Solution.FullName);
            GetLLMServiceConfig();
            GetFolderAndFuncIORecFilePath();
        }

        private void GetLLMServiceConfig()
        {
            apiType = package.page.ApiType;
            apiEndpoint = package.page.ApiEndpoint;
            apiVersion = package.page.ApiVersion;
            apiKey = package.page.ApiKey;
            deploymentName = package.page.DeploymentName;
            modelName = package.page.ModelName;
            if (String.IsNullOrEmpty(apiEndpoint) || String.IsNullOrEmpty(apiKey) || String.IsNullOrEmpty(deploymentName))
            {
                // Show a message box to prove we were here
                VsShellUtilities.ShowMessageBox(
                    this.package,
                    "apiEndpoint or apiKey or deploymentName is null or empty please check it in Tools->Options->UTGenSettings",
                    "UTGen Error!",
                    OLEMSGICON.OLEMSGICON_INFO,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }

            string message = $"apiType: {apiType}\n apiEndpoint: {apiEndpoint}\n apiVersion: {apiVersion}\n apiKey: {apiKey}\n deploymentName: {deploymentName}\n modelName: {modelName}";
            string title = "UTGen executing";

            // Show a message box to prove we were here
            VsShellUtilities.ShowMessageBox(
                this.package,
                message,
                title,
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        private void GetFolderAndFuncIORecFilePath()
        {
            copilotPlaygroundPath = Path.Combine(solutionDirectory, ".CSharpUTGen");
            outputCalcProjPath = Path.Combine(solutionDirectory, ".CSharpUTGenOutputCalcProj");

            funcIORecFiles = Directory.GetFiles(solutionDirectory, "funcIORec.json", SearchOption.AllDirectories)
                .Where(file => !file.Contains(".CSharpUTGen")).ToArray();

            if (funcIORecFiles.Length == 0)
            {
                VsShellUtilities.ShowMessageBox(
                    package.dte as System.IServiceProvider,
                    "Cannot find the funcIORec.json file in the Visual Studio solution, please make sure you have recorded the C# function call using our C# utils.",
                    "File Not Found",
                    OLEMSGICON.OLEMSGICON_CRITICAL,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
            else if (funcIORecFiles.Length > 1)
            {
                VsShellUtilities.ShowMessageBox(
                    package.dte as System.IServiceProvider,
                    "Found more than one funcIORec.json file in the Visual Studio solution, please navigate to the C# project which needs to generate UT code this time or just delete other funcIORec.json files.",
                    "Multiple Files Found",
                    OLEMSGICON.OLEMSGICON_WARNING,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }

        private async Task<IWpfTextView> GetActiveTextViewAsync()
        {
            var textManager = await package.GetServiceAsync(typeof(SVsTextManager)) as IVsTextManager;
            textManager.GetActiveView(1, null, out IVsTextView vTextView);

            IVsEditorAdaptersFactoryService editorAdapter =
                await package.GetServiceAsync(typeof(IVsEditorAdaptersFactoryService)) as IVsEditorAdaptersFactoryService;

            return editorAdapter.GetWpfTextView(vTextView);
        }

        private async Task<SnapshotSpan?> GetMethodCodePositionRangeAsync()
        {
            var textView = await GetActiveTextViewAsync();

            var caretPosition = textView.Caret.Position.BufferPosition;

            /*
            // Assuming you have a way to get your Roslyn Document object  
            var document = GetRoslynDocument();

            // Now you can use Roslyn to analyze the document and find the method at the caret position  
            // This is a simplified example, and actual implementation will depend on your specific needs  
            var syntaxRoot = await document.GetSyntaxRootAsync();
            var methodNode = syntaxRoot.FindToken(caretPosition.Position).Parent.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();

            if (methodNode != null)
            {
                var span = methodNode.Span;
                return new SnapshotSpan(viewHost.TextView.TextSnapshot, new Span(span.Start, span.Length));
            }
            */

            return null;
        }
    }
}
