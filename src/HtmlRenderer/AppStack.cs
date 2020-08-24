using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.S3;

namespace HtmlRenderer
{
    public class AppStack : Stack
    {
        internal AppStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            // https://github.com/shelfio/chrome-aws-lambda-layer
            var chromeLayer = new LayerVersion(this, "ChromeLayer", new LayerVersionProps()
            {
                Code = AssetCode.FromAsset("chrome_aws_lambda.zip"),
                CompatibleRuntimes = new Runtime[] { Runtime.NODEJS_12_X }
            });

            var renderImageBucket = new Bucket(this, "RenderImageBucket", new BucketProps()
            {
            });

            var renderHtmlToS3Function = new Function(this, "RenderHtmlToS3Function", new FunctionProps()
            {
                Runtime = Runtime.NODEJS_12_X,
                MemorySize = 1024,
                Timeout = Duration.Seconds(10),
                Code = Code.FromAsset("handlers"),
                Handler = "render-html-to-s3.handler",
                Environment = new Dictionary<string, string>()
                {
                    ["BucketName"] = renderImageBucket.BucketName,
                },
                Layers = new ILayerVersion[] { chromeLayer },
            });
            renderImageBucket.GrantReadWrite(renderHtmlToS3Function);
        }
    }
}
