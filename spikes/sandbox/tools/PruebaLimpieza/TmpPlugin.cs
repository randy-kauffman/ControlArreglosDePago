using System;
using Microsoft.Xrm.Sdk;

namespace Spk.Tmp
{
    public class TmpPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            context.OutputParameters["Salida"] = "ok";
        }
    }
}
