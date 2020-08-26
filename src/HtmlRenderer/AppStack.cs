using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.EFS;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Deployment;
using System.Collections.Generic;
using EFS = Amazon.CDK.AWS.EFS;
using Lambda = Amazon.CDK.AWS.Lambda;

namespace HtmlRenderer
{
    public class AppStack : Stack
    {
        internal AppStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            // 面倒くさいのでスタックは分けない。
            var vpc = new Vpc(this, "Vpc");
            var efs = new EFS.FileSystem(this, "Efs", new EFS.FileSystemProps()
            {
                Vpc = vpc,
            });
            var efsAccessPoint = new EFS.AccessPoint(this, "EfsAccessPoint", new EFS.AccessPointProps()
            {
                FileSystem = efs,
                // 他の設定そのままで "/" では書き込み権限が得られていなかった。
                Path = "/lambda",
                // ファイルIOに用いるユーザーとディレクトリ作成時権限の設定は必須である様子。
                // CDKが既定のユーザーを構成してくれるようなことはない。
                PosixUser = new PosixUser()
                {
                    Gid = "1001",
                    Uid = "1001",
                },
                CreateAcl = new Acl()
                {
                    OwnerGid = "1001",
                    OwnerUid = "1001",
                    Permissions = "755",
                },
            });

            // Assets
            // https://docs.aws.amazon.com/cdk/api/latest/docs/aws-s3-assets-readme.html
            // vs
            // https://docs.aws.amazon.com/cdk/api/latest/docs/aws-s3-deployment-readme.html
            // 静的にS3にファイルを残し、スタックのデプロイ後にDataSyncでEFSに転送するのでDeployment。
            var assetBucket = new Bucket(this, "AssetBucket", new BucketProps()
            {
            });
            new BucketDeployment(this, "AssetBucketDeployment", new BucketDeploymentProps()
            {
                Sources = new ISource[] { Source.Asset("assets") },
                DestinationBucket = assetBucket,
            });

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
                Vpc = vpc,
                Runtime = Runtime.NODEJS_12_X,
                MemorySize = 1024,
                Timeout = Duration.Seconds(10),
                Code = Code.FromAsset("handlers"),
                Handler = "render-html-to-s3.handler",
                Environment = new Dictionary<string, string>()
                {
                    ["BucketName"] = renderImageBucket.BucketName,
                    ["EfsMountPath"] = "/mnt/efs",
                },
                Layers = new ILayerVersion[] { chromeLayer },
                Filesystem = Lambda.FileSystem.FromEfsAccessPoint(efsAccessPoint, "/mnt/efs"),
            });
            // VPCやEFSに関してはCDK上の関連から
            // セキュリティグループや既定のロールへのインラインポリシーが構成される。
            // S3バケットはCDK上の関連はないため明に権限を付与する。
            renderImageBucket.GrantReadWrite(renderHtmlToS3Function);
        }
    }
}
