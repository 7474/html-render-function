using Amazon.CDK;
using System;
using System.Collections.Generic;
using System.Linq;

namespace App
{
    sealed class Program
    {
        public static void Main(string[] args)
        {
            var app = new App();
            new AppStack(app, "AppStack");
            app.Synth();
        }
    }
}
