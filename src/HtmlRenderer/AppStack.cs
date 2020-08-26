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
            var efsUser = new PosixUser()
            {
                Gid = "1001",
                Uid = "1001",
            };
            var efsCreateAcl = new Acl()
            {
                OwnerGid = "1001",
                OwnerUid = "1001",
                Permissions = "755",
            };
            var efsAccessPoint = new EFS.AccessPoint(this, "EfsAccessPoint", new EFS.AccessPointProps()
            {
                FileSystem = efs,
                // 他の設定そのままで "/" では書き込み権限が得られていなかった。
                // CDK上ではなく、NFSマウント後にルートユーザーで権限を操作すればよい。
                // （ルートディレクトリは既定でルートユーザーが所有している状態）
                // See. https://docs.aws.amazon.com/ja_jp/efs/latest/ug/using-fs.html
                //      https://docs.aws.amazon.com/ja_jp/efs/latest/ug/accessing-fs-nfs-permissions-per-user-subdirs.html
                Path = "/",
                // ファイルIOに用いるユーザーとディレクトリ作成時権限の設定は必須である様子。
                // CDKが既定のユーザーを構成してくれるようなことはない。
                // -> ↑嘘。必要がなければ構成しなくても問題ない。所詮はNFSなので、権限が他のユーザーに解放されているディレクトリは操作できる。はず。
                PosixUser = efsUser,
                CreateAcl = efsCreateAcl,
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

            // 踏み台
            var bastion = new BastionHostLinux(this, "Bastion", new BastionHostLinuxProps()
            {
                InstanceType = InstanceType.Of(InstanceClass.BURSTABLE3, InstanceSize.NANO),
                Vpc = vpc,
            });
            assetBucket.GrantRead(bastion);
            // https://docs.aws.amazon.com/cdk/api/latest/docs/aws-efs-readme.html
            efs.Connections.AllowDefaultPortFrom(bastion);
            bastion.Instance.UserData.AddCommands(
                "yum check-update -y",                          // Ubuntu: apt-get -y update
                "yum upgrade -y",                               // Ubuntu: apt-get -y upgrade
                "yum install -y amazon-efs-utils",              // Ubuntu: apt-get -y install amazon-efs-utils
                "yum install -y nfs-utils",                     // Ubuntu: apt-get -y install nfs-common
                "file_system_id_1=" + efs.FileSystemId,
                "efs_mount_point_1=/mnt/efs/fs1",
                "mkdir -p \"${efs_mount_point_1}\"",
                "test -f \"/sbin/mount.efs\" && echo \"${file_system_id_1}:/ ${efs_mount_point_1} efs defaults,_netdev\" >> /etc/fstab || " +
                "echo \"${file_system_id_1}.efs." + Stack.Of(this).Region + ".amazonaws.com:/ ${efs_mount_point_1} nfs4 nfsvers=4.1,rsize=1048576,wsize=1048576,hard,timeo=600,retrans=2,noresvport,_netdev 0 0\" >> /etc/fstab",
                "mount -a -t efs,nfs4 defaults",
                "chmod go+rw /mnt/efs/fs1"
            );

            new CfnOutput(this, "BastionInstanceId", new CfnOutputProps()
            {
                ExportName = "BastionInstanceId",
                Value = bastion.InstanceId,
            });
            new CfnOutput(this, "AssetBucketName", new CfnOutputProps()
            {
                ExportName = "AssetBucketName",
                Value = assetBucket.BucketName,
            });
        }
    }
}
