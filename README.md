# html-render-function

以下のようなことをお試ししているリポジトリです。

- AWS Lambda上でヘッドレスChromeを使ってHTMLレンダリング
- AWS Lambda + 他のAWSリソースの接続を試す

## 構成

CDK（C#）です。

* `dotnet build src` compile this app
* `cdk deploy`       deploy this stack to your default AWS account/region
* `cdk diff`         compare deployed stack with current state
* `cdk synth`        emits the synthesized CloudFormation template

## リンク

- https://github.com/shelfio/chrome-aws-lambda-layer
