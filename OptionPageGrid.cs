using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSIXHelloWorldProject
{
    public class OptionPageGrid : DialogPage
    {
        [Category("UTGenSettings")]
        [DisplayName("ApiEndpoint")]
        [Description("ApiEndpoint")]
        public string ApiEndpoint { get; set; }

        [Category("UTGenSettings")]
        [DisplayName("ApiKey")]
        [Description("ApiKey")]
        public string ApiKey { get; set; }

        [Category("UTGenSettings")]
        [DisplayName("DeploymentName")]
        [Description("DeploymentName")]
        public string DeploymentName { get; set; }
    }
}
