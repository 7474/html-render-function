const crypto = require("crypto");
const chromium = require("chrome-aws-lambda");
const aws = require("aws-sdk");

const s3 = new aws.S3();

function md5hex(str /*: string */) {
  const md5 = crypto.createHash("md5");
  return md5.update(str, "binary").digest("hex");
}

let browser;

exports.handler = async (event, context) => {
  const html = event.html;
  const htmlMd5 = md5hex(html);
  const now = Date.now();
  const bucketName = process.env.BucketName;
  console.log(html);
  console.log(htmlMd5);
  console.log(bucketName);

  // 立ち上げに時間がかかる（1024MBメモリで3秒ほど）ので実行毎ではなく1回立ち上げる。
  if (!browser) {
    console.log("Start launch browser.");
    browser = await chromium.puppeteer.launch({
      args: chromium.args,
      defaultViewport: chromium.defaultViewport,
      executablePath: await chromium.executablePath,
      headless: chromium.headless,
    });
    console.log("End launch browser.");
  }

  console.log("Start browse.");
  let page = await browser.newPage();
  await page.setContent(html);
  console.log("End browse.");

  console.log("Start screenshot.");
  const screenshot = await page.screenshot({
    type: "png",
    fullPage: true,
    omitBackground: true,
    encoding: "binary",
  });
  console.log("End screenshot.");

  page.close();

  // 毎回立ち上げるならbrowserも掃除する
  // await browser.close();

  console.log("Start put S3.");
  await s3
    .upload({
      Bucket: bucketName,
      Key: now + "-" + htmlMd5 + ".png",
      Body: screenshot,
      ContentType: "image/png",
    })
    .promise()
    .then((res) => {
      console.log("End put S3.");
      console.log(res.Location);
    })
    .catch((e) => Promise.reject(e));
};
