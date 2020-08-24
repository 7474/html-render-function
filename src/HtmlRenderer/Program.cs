using Amazon.CDK;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HtmlRenderer
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var app = new App();
            new AppStack(app, "HtmlRendererAppStack");
            app.Synth();
        }
    }
}
